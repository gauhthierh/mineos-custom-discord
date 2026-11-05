using System.Net;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using MineOS.Application.Interfaces;
using MineOS.Infrastructure.Services;

namespace MineOS.Tests.Unit;

public class DiscordNotificationDeliveryTests
{
    private sealed class Handler(Func<int, HttpResponseMessage>? respond = null) : HttpMessageHandler
    {
        public readonly Channel<(string Url, string Body)> Requests = Channel.CreateUnbounded<(string, string)>();
        public int Count;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var count = Interlocked.Increment(ref Count);
            var body = await request.Content!.ReadAsStringAsync(ct);
            Requests.Writer.TryWrite((request.RequestUri!.ToString(), body));
            return respond?.Invoke(count) ?? new(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }
    }

    private static ServiceProvider Build(DiscordNotificationConfiguration config, Handler handler)
    {
        var settings = new Mock<IDiscordNotificationSettings>();
        settings.Setup(s => s.GetAsync(It.IsAny<CancellationToken>(), true)).ReturnsAsync(config);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(settings.Object);
        services.AddHttpClient(DiscordWebhookService.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
        services.AddSingleton<DiscordWebhookService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task StatusAndBackups_AreDeliveredToSeparateChannels()
    {
        var handler = new Handler();
        using var provider = Build(DiscordNotificationSettingsTests.Configuration(), handler);
        var service = provider.GetRequiredService<DiscordWebhookService>();
        await service.StartAsync(default);
        try
        {
            // No rule for create; it must never be sent.
            service.QueueEvent(DiscordEvents.Catalog.Single(e => e.Id == DiscordEvents.Created).Sample("survie"));
            service.QueueEvent(DiscordEvents.Catalog.Single(e => e.Id == DiscordEvents.Started).Sample("survie"));
            service.QueueEvent(DiscordEvents.Catalog.Single(e => e.Id == "backup.completed").Sample("survie"));
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var status = await handler.Requests.Reader.ReadAsync(timeout.Token);
            var backup = await handler.Requests.Reader.ReadAsync(timeout.Token);
            Assert.Contains("/123/", status.Url);
            Assert.Contains("/456/", backup.Url);
            Assert.Contains("Serveur", System.Text.Json.JsonDocument.Parse(status.Body).RootElement.GetProperty("embeds")[0].GetProperty("title").GetString());
            Assert.Equal(2, handler.Count);
        }
        finally { await service.StopAsync(default); }
    }

    [Theory]
    [InlineData("global")]
    [InlineData("destination")]
    [InlineData("rule")]
    [InlineData("server")]
    public async Task DisabledOrFilteredEvents_DoNotSend(string mode)
    {
        var config = DiscordNotificationSettingsTests.Configuration();
        if (mode == "global") config = config with { Enabled = false };
        if (mode == "destination") config.Destinations[0] = config.Destinations[0] with { Enabled = false };
        if (mode == "rule") config.Rules[0] = config.Rules[0] with { Enabled = false };
        if (mode == "server") config.Rules[0] = config.Rules[0] with { ServerNames = ["another-server"] };
        var handler = new Handler();
        using var provider = Build(config, handler);
        var service = provider.GetRequiredService<DiscordWebhookService>();
        // The in-memory settings task completes synchronously, so StartAsync processes the queued event before yielding.
        service.QueueEvent(DiscordEvents.Catalog.Single(e => e.Id == DiscordEvents.Started).Sample("survie"));
        await service.StartAsync(default);
        await service.StopAsync(default);
        Assert.Equal(0, handler.Count);
    }

    [Fact]
    public async Task RateLimitedWebhook_RetriesUsingDiscordJsonDelay()
    {
        var handler = new Handler(count => count == 1
            ? new(HttpStatusCode.TooManyRequests) { Content = new StringContent("{\"retry_after\":0.05}") }
            : new(HttpStatusCode.OK));
        using var provider = Build(DiscordNotificationSettingsTests.Configuration(), handler);
        var service = provider.GetRequiredService<DiscordWebhookService>();
        var result = await service.TestAsync(new("status"), default);
        Assert.True(result.Success);
        Assert.Equal(2, handler.Count);
        Assert.True(Assert.Single(service.GetJournal().Entries).Success);
    }

    [Fact]
    public async Task InvalidWebhook_IsReportedWithoutRetryingOrExposingSecrets()
    {
        var handler = new Handler(_ => new(HttpStatusCode.NotFound)
            { Content = new StringContent("secret-token must not be shown") });
        using var provider = Build(DiscordNotificationSettingsTests.Configuration(), handler);
        var service = provider.GetRequiredService<DiscordWebhookService>();
        var result = await service.TestAsync(new("status"), default);
        Assert.False(result.Success);
        Assert.Equal(1, handler.Count);
        Assert.DoesNotContain("secret-token", result.Detail);
        Assert.False(Assert.Single(service.GetJournal().Entries).Success);
    }

    [Fact]
    public async Task ExplicitTest_CanRunWhileAutomaticDeliveryIsPaused()
    {
        var handler = new Handler();
        using var provider = Build(DiscordNotificationSettingsTests.Configuration() with { Enabled = false }, handler);
        var service = provider.GetRequiredService<DiscordWebhookService>();
        Assert.True((await service.TestAsync(new("backups", "backups", "backup.failed"), default)).Success);
        var (_, body) = await handler.Requests.Reader.ReadAsync();
        using var document = System.Text.Json.JsonDocument.Parse(body);
        Assert.Contains("Test de notification", document.RootElement.GetProperty("content").GetString());
    }

    [Fact]
    public void FullQueue_ReportsDroppedEvents()
    {
        using var provider = Build(DiscordNotificationSettingsTests.Configuration(), new Handler());
        var service = provider.GetRequiredService<DiscordWebhookService>();
        var evt = DiscordEvents.Catalog[0].Sample("survie");
        for (var i = 0; i < 103; i++) service.QueueEvent(evt);
        Assert.Equal(3, service.GetJournal().DroppedEvents);
    }
}
