using System.Text.Json;
using Moq;
using MineOS.Application.Interfaces;
using MineOS.Infrastructure.Services;

namespace MineOS.Tests.Unit;

public class DiscordNotificationSettingsTests
{
    private const string Webhook = "https://discord.com/api/webhooks/123/secret-token";
    private readonly Dictionary<string, string?> _values = new();
    private readonly Mock<ISettingsService> _settings = new();
    private readonly DiscordNotificationSettings _service;

    public DiscordNotificationSettingsTests()
    {
        _settings.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string key, CancellationToken _) => Task.FromResult(_values.GetValueOrDefault(key)));
        _settings.Setup(s => s.SetAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((string key, string? value, CancellationToken _) => _values[key] = value)
            .Returns(Task.CompletedTask);
        _service = new(_settings.Object);
    }

    public static DiscordNotificationConfiguration Configuration() => new()
    {
        Destinations = [new() { Id = "status", Name = "Statut", WebhookUrl = Webhook },
            new() { Id = "backups", Name = "Sauvegardes", WebhookUrl = "https://discord.com/api/webhooks/456/another-token" }],
        Rules = [new() { Id = "status", Name = "Statut", EventTypes = [DiscordEvents.Started, DiscordEvents.Stopped], DestinationIds = ["status"] },
            new() { Id = "backups", Name = "Sauvegardes", EventTypes = ["backup.completed", "backup.failed"], DestinationIds = ["backups"] }]
    };

    [Fact]
    public async Task LegacyConfiguration_IsImportedWithoutExposingToken()
    {
        _values[SettingsService.Keys.DiscordWebhookUrl] = Webhook;
        var publicConfig = await _service.GetAsync(default);
        Assert.Null(Assert.Single(publicConfig.Destinations).WebhookUrl);
        Assert.True(publicConfig.Destinations[0].HasWebhook);
        Assert.Equal(DiscordEvents.Catalog.Count, Assert.Single(publicConfig.Rules).EventTypes.Count);
        Assert.Equal(Webhook, (await _service.GetAsync(default, true)).Destinations[0].WebhookUrl);
    }

    [Fact]
    public async Task Save_RedactsSecretsAndPreservesThemWhenEditingRules()
    {
        var saved = await _service.SaveAsync(Configuration(), default);
        Assert.All(saved.Destinations, d => { Assert.True(d.HasWebhook); Assert.Null(d.WebhookUrl); });
        saved.Rules[0] = saved.Rules[0] with { MessageTemplate = "{server} est en ligne." };
        await _service.SaveAsync(saved, default);
        var stored = await _service.GetAsync(default, true);
        Assert.Equal(Webhook, stored.Destinations[0].WebhookUrl);
        Assert.Equal("{server} est en ligne.", stored.Rules[0].MessageTemplate);
        Assert.DoesNotContain("secret-token", JsonSerializer.Serialize(await _service.GetAsync(default)));
    }

    [Fact]
    public async Task EmptyCustomConfiguration_DoesNotReactivateLegacyWebhook()
    {
        _values[SettingsService.Keys.DiscordWebhookUrl] = Webhook;
        await _service.SaveAsync(new() { Enabled = false }, default);
        var loaded = await _service.GetAsync(default, true);
        Assert.False(loaded.Enabled);
        Assert.Empty(loaded.Destinations);
        Assert.Empty(loaded.Rules);
    }

    [Fact]
    public async Task InvalidSave_DoesNotOverwriteExistingConfiguration()
    {
        await _service.SaveAsync(Configuration(), default);
        var previous = _values[DiscordNotificationSettings.SettingKey];
        var invalid = Configuration();
        invalid.Rules[0].DestinationIds.Add("unknown");
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync(invalid, default));
        Assert.Equal(previous, _values[DiscordNotificationSettings.SettingKey]);
    }

    [Fact]
    public async Task CorruptConfiguration_DoesNotFallBackToLegacy()
    {
        _values[SettingsService.Keys.DiscordWebhookUrl] = Webhook;
        _values[DiscordNotificationSettings.SettingKey] = "broken-json";
        await Assert.ThrowsAsync<JsonException>(() => _service.GetAsync(default));
    }

    [Theory]
    [InlineData("http://discord.com/api/webhooks/1/token")]
    [InlineData("https://127.0.0.1/api/webhooks/1/token")]
    [InlineData("https://discord.com.evil.test/api/webhooks/1/token")]
    [InlineData("https://discord.com@evil.test/api/webhooks/1/token")]
    [InlineData("https://evil.test@discord.com/api/webhooks/1/token")]
    [InlineData("https://discord.com:444/api/webhooks/1/token")]
    [InlineData("https://discord.com/api/webhooks/1/token?redirect=evil")]
    [InlineData("https://discord.com/api/webhooks/1/token#fragment")]
    [InlineData("https://discord.com/api/webhooks/1/token/messages/123")]
    [InlineData("https://discord.com/api/webhooks/1/%74oken")]
    public async Task RejectsUnsafeWebhookUrls(string url)
    {
        var config = Configuration();
        config.Destinations[0] = config.Destinations[0] with { WebhookUrl = url };
        await Assert.ThrowsAsync<ArgumentException>(() => _service.SaveAsync(config, default));
        Assert.Empty(_values);
    }

    [Theory]
    [InlineData("https://discord.com/api/webhooks/123/token")]
    [InlineData("https://discordapp.com/api/webhooks/123/token")]
    [InlineData("https://discord.com/api/v10/webhooks/123/token")]
    public void AcceptsOfficialWebhookUrls(string url) => Assert.True(DiscordNotificationSettings.IsWebhookUrl(url));

    [Fact]
    public void RejectsUnknownEventsAndVariables()
    {
        var rule = Configuration().Rules[0];
        Assert.Throws<ArgumentException>(() => DiscordNotificationSettings.ValidateRule(rule with { EventTypes = ["unknown"] }));
        Assert.Throws<ArgumentException>(() => DiscordNotificationSettings.ValidateRule(rule with { MessageTemplate = "{password}" }));
        Assert.Throws<ArgumentException>(() => DiscordNotificationSettings.ValidateRule(rule with { Color = "red" }));
        Assert.Throws<ArgumentException>(() => DiscordNotificationSettings.ValidateRule(rule with { ServerNames = [null!] }));
    }
}
