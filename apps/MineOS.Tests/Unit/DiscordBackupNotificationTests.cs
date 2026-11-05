using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MineOS.Application.Interfaces;
using MineOS.Application.Options;
using MineOS.Infrastructure.Persistence;
using MineOS.Infrastructure.Services;

namespace MineOS.Tests.Unit;

public class DiscordBackupNotificationTests
{
    [Fact]
    public async Task RejectedRestore_EmitsStartedAndFailedEventsWithoutRunningExternalTools()
    {
        var root = Path.Combine(Path.GetTempPath(), "mineos-discord-" + Guid.NewGuid().ToString("N"));
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var discord = new Mock<IDiscordWebhookService>();
        var events = new List<DiscordEvent>();
        discord.Setup(d => d.QueueEvent(It.IsAny<DiscordEvent>())).Callback<DiscordEvent>(events.Add);
        var process = new Mock<IProcessManager>();
        var service = new BackupService(NullLogger<BackupService>.Instance,
            Options.Create(new HostOptions { BaseDirectory = root }), db,
            Mock.Of<IFeatureUsageTracker>(), Mock.Of<ITelemetryService>(), process.Object, discord.Object);

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => service.RestoreBackupAsync("survie", "now", default));
        Assert.Equal(new[] { "restore.started", "restore.failed" }, events.Select(e => e.EventType));
        Assert.All(events, e => Assert.Equal("survie", e.ServerName));
        process.Verify(p => p.IsServerRunningAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.False(Directory.Exists(root));
    }
}
