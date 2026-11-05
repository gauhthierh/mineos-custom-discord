using ImageMagick;
using MineOS.Application.Interfaces;

namespace MineOS.Infrastructure.Services;

/// <summary>Decode bounded raster uploads and produce Minecraft's 64x64 PNG icon.</summary>
public sealed class ServerIconProcessor : IServerIconProcessor
{
    private const int MaxBytes = 5 * 1024 * 1024;

    static ServerIconProcessor()
    {
        ResourceLimits.Width = 8192;
        ResourceLimits.Height = 8192;
        ResourceLimits.Memory = 128 * 1024 * 1024;
        ResourceLimits.Disk = 0;
        ResourceLimits.Thread = 2;
    }

    public async Task<byte[]> CreatePngAsync(Stream input, CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
                throw new ArgumentException("Icon too large. Maximum is 5MB.");
            buffer.Write(chunk, 0, read);
        }
        var format = DetectFormat(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        var settings = new MagickReadSettings { Format = format, FrameIndex = 0, FrameCount = 1 };
        buffer.Position = 0;
        try
        {
            ct.ThrowIfCancellationRequested();
            using var image = new MagickImage(buffer, settings);
            if (image.Width > 8192 || image.Height > 8192)
                throw new ArgumentException("Icon dimensions must not exceed 8192 pixels.");
            image.Resize(new MagickGeometry(64, 64) { IgnoreAspectRatio = true });
            image.Strip();
            ct.ThrowIfCancellationRequested();
            return image.ToByteArray(MagickFormat.Png);
        }
        catch (MagickException)
        {
            throw new ArgumentException("File must be a valid raster image within the size limits.");
        }
    }

    // Force a raster decoder; never allow vector, URL, document or script delegates.
    private static MagickFormat DetectFormat(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return MagickFormat.Png;
        if (data.StartsWith(new byte[] { 255, 216, 255 })) return MagickFormat.Jpeg;
        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8)) return MagickFormat.Gif;
        if (data.StartsWith("BM"u8)) return MagickFormat.Bmp;
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data.Slice(8, 4).SequenceEqual("WEBP"u8)) return MagickFormat.WebP;
        if (data.StartsWith(new byte[] { 73, 73, 42, 0 }) || data.StartsWith(new byte[] { 77, 77, 0, 42 })) return MagickFormat.Tiff;
        if (data.StartsWith(new byte[] { 0, 0, 1, 0 })) return MagickFormat.Ico;
        throw new ArgumentException("File must be a valid raster image (PNG, JPG, GIF, BMP, WebP, TIFF or ICO).");
    }
}
