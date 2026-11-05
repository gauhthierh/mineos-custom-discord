using System.Text.Json;
using MineOS.Application.Interfaces;
using MineOS.Infrastructure.Services;

namespace MineOS.Tests.Unit;

public class DiscordMessageRendererTests
{
    private static DiscordEvent Event(string eventType = DiscordEvents.Started, string server = "survie") =>
        new("Serveur en ligne", "Tout va bien.", DiscordEventLevel.Success, server, DateTimeOffset.UtcNow, eventType);

    [Fact]
    public void RoutingUsesStableEventIdsAndExactServerNames()
    {
        var rule = DiscordNotificationSettingsTests.Configuration().Rules[0] with { ServerNames = ["survie"] };
        Assert.True(DiscordMessageRenderer.Matches(rule, Event()));
        Assert.False(DiscordMessageRenderer.Matches(rule, Event("backup.completed")));
        Assert.False(DiscordMessageRenderer.Matches(rule, Event(server: "survie-2")));
        Assert.False(DiscordMessageRenderer.Matches(rule with { Enabled = false }, Event()));
        Assert.True(DiscordMessageRenderer.Matches(rule with { ServerNames = [] }, Event(server: "futur-serveur")));
    }

    [Fact]
    public void Templates_AreSubstitutedOnceAndMentionsAreDisabled()
    {
        var evt = Event(server: "{message} @everyone");
        var rule = new DiscordNotificationRule { TitleTemplate = "Statut {server}", MessageTemplate = "{message} ({event})", Color = "#123456", ShowServer = false, ShowTimestamp = false };
        using var document = JsonDocument.Parse(DiscordMessageRenderer.BuildPayload(evt, rule, "Noryandor"));
        var root = document.RootElement;
        var embed = root.GetProperty("embeds")[0];
        Assert.Equal("Noryandor", root.GetProperty("username").GetString());
        Assert.Equal("Statut {message} @everyone", embed.GetProperty("title").GetString());
        Assert.Equal(0x123456, embed.GetProperty("color").GetInt32());
        Assert.Equal(0, root.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
        Assert.False(embed.TryGetProperty("timestamp", out _));
        Assert.False(embed.TryGetProperty("fields", out _));
    }

    [Fact]
    public void OversizedRuntimeMessages_AreTruncatedForDiscord()
    {
        var evt = Event() with { Title = new string('x', 1000), Message = new string('y', 10000), ServerName = new string('z', 2000) };
        using var embedDocument = JsonDocument.Parse(DiscordMessageRenderer.BuildPayload(evt));
        var embed = embedDocument.RootElement.GetProperty("embeds")[0];
        Assert.Equal(256, embed.GetProperty("title").GetString()!.Length);
        Assert.Equal(4096, embed.GetProperty("description").GetString()!.Length);
        Assert.Equal(1024, embed.GetProperty("fields")[0].GetProperty("value").GetString()!.Length);
        using var textDocument = JsonDocument.Parse(DiscordMessageRenderer.BuildPayload(evt, new() { Format = "text" }));
        Assert.Equal(2000, textDocument.RootElement.GetProperty("content").GetString()!.Length);
        Assert.False(textDocument.RootElement.TryGetProperty("embeds", out _));
    }

    [Theory]
    [InlineData("backup", "failed", "backup.failed")]
    [InlineData("restore", "completed", "restore.completed")]
    [InlineData("modrinth-modpack-install", "cancelled", "installation.cancelled")]
    [InlineData("future-task", "completed", "job.completed")]
    public void JobsHaveStableEventTypes(string type, string outcome, string expected)
    {
        Assert.Equal(expected, DiscordEvents.ForJob(type, outcome));
        Assert.Contains(DiscordEvents.Catalog, e => e.Id == expected);
    }
}
