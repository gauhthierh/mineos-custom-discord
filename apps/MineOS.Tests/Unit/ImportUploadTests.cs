using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MineOS.Application.Interfaces;
using MineOS.Application.Options;
using MineOS.Infrastructure.Services;
using Moq;

namespace MineOS.Tests.Unit;

public sealed class ImportUploadTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"mineos-upload-{Guid.NewGuid():N}");
    private string Imports => Path.Combine(_root, "import");
    private ImportService Service => new(Options.Create(new HostOptions { BaseDirectory = _root }), NullLogger<ImportService>.Instance);

    [Fact]
    public async Task Upload_StreamsNonSeekableContentAndPublishesOnlyAfterCompletion()
    {
        Directory.CreateDirectory(Imports);
        var target = Path.Combine(Imports, "server.zip");
        await File.WriteAllTextAsync(target, "previous archive");
        using var source = new GeneratedStream(4 * 1024 * 1024, beforeRead: () =>
        {
            Assert.Equal("previous archive", File.ReadAllText(target));
            Assert.Single(Directory.GetFiles(Imports, ".upload-*.tmp"));
        });
        await Service.SaveImportAsync("server.zip", source, default, source.Size);
        Assert.Equal(source.Size, new FileInfo(target).Length);
        Assert.InRange(source.LargestRead, 1, 131072);
        Assert.Empty(Directory.GetFiles(Imports, ".upload-*.tmp"));
        await using var stored = File.OpenRead(target);
        Assert.Equal(0x5a, stored.ReadByte());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InterruptedUpload_RemovesTemporaryFileAndPreservesExistingArchive(bool cancel)
    {
        Directory.CreateDirectory(Imports);
        var target = Path.Combine(Imports, "server.zip");
        await File.WriteAllTextAsync(target, "previous archive");
        using var cts = new CancellationTokenSource();
        var reads = 0;
        using var source = new GeneratedStream(1024 * 1024, beforeRead: () =>
        {
            if (++reads == 1) return;
            if (cancel) cts.Cancel();
            else throw new IOException("Simulated interrupted connection");
        });
        if (cancel)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service.SaveImportAsync("server.zip", source, cts.Token));
        else
            await Assert.ThrowsAsync<IOException>(() => Service.SaveImportAsync("server.zip", source, cts.Token));
        Assert.Equal("previous archive", await File.ReadAllTextAsync(target));
        Assert.Empty(Directory.GetFiles(Imports, ".upload-*.tmp"));
    }

    [Fact]
    public async Task TruncatedUpload_Uses64BitContentLengthAndDoesNotPublish()
    {
        using var source = new GeneratedStream(1024);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            Service.SaveImportAsync("server.zip", source, default, 10L * 1024 * 1024 * 1024));
        Assert.Empty(Directory.GetFiles(Imports));
    }

    [Fact]
    public async Task PendingUploads_AreNotListedAsReadyImports()
    {
        Directory.CreateDirectory(Imports);
        await File.WriteAllTextAsync(Path.Combine(Imports, ".upload-123.tmp"), "pending");
        await File.WriteAllTextAsync(Path.Combine(Imports, "ready.zip"), "ready");
        var service = new HostService(Options.Create(new HostOptions { BaseDirectory = _root }),
            NullLogger<HostService>.Instance, Mock.Of<IProcessManager>(), Mock.Of<IMonitoringService>(), Mock.Of<IServerService>());
        var result = await service.GetImportsAsync(default);
        Assert.Equal("ready.zip", Assert.Single(result).Filename);
    }

    [Theory]
    [InlineData("../escape.zip")]
    [InlineData("not-an-archive.exe")]
    [InlineData("")]
    public async Task InvalidName_IsRejectedBeforeCreatingFiles(string name)
    {
        using var source = new GeneratedStream(1024);
        await Assert.ThrowsAsync<ArgumentException>(() => Service.SaveImportAsync(name, source, default));
        Assert.False(Directory.Exists(Imports));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private sealed class GeneratedStream(long size, Action? beforeRead = null) : Stream
    {
        private long _remaining = size;
        public long Size { get; } = size;
        public int LargestRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            beforeRead?.Invoke();
            LargestRead = Math.Max(LargestRead, count);
            var read = (int)Math.Min(count, _remaining);
            buffer.AsSpan(offset, read).Fill(0x5a);
            _remaining -= read;
            return read;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            beforeRead?.Invoke();
            LargestRead = Math.Max(LargestRead, buffer.Length);
            var read = (int)Math.Min(buffer.Length, _remaining);
            buffer.Span[..read].Fill(0x5a);
            _remaining -= read;
            return ValueTask.FromResult(read);
        }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
