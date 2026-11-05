using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MineOS.Application.Interfaces;

namespace MineOS.Infrastructure.Services;

/// <summary>Bounded asynchronous delivery. Discord failures never block Minecraft operations.</summary>
public sealed class DiscordWebhookService : BackgroundService, IDiscordWebhookService, IDiscordNotificationDelivery
{
    public const string HttpClientName = "discord-webhook";
    private readonly Channel<DiscordEvent> _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<DiscordWebhookService> _logger;
    private readonly ConcurrentQueue<DiscordDeliveryEntry> _journal = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private DateTimeOffset _nextSend;
    private long _droppedEvents;

    public DiscordWebhookService(IServiceScopeFactory scopeFactory, IHttpClientFactory httpClientFactory,
        ILogger<DiscordWebhookService> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _queue = Channel.CreateBounded<DiscordEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true
        }, _ => Interlocked.Increment(ref _droppedEvents));
    }

    public void QueueEvent(DiscordEvent evt) => _queue.Writer.TryWrite(evt);

    public DiscordDeliveryJournal GetJournal() => new(_journal.Reverse().ToArray(), Interlocked.Read(ref _droppedEvents));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var evt in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var configuration = await scope.ServiceProvider.GetRequiredService<IDiscordNotificationSettings>()
                        .GetAsync(stoppingToken, includeSecrets: true);
                    if (!configuration.Enabled) continue;
                    foreach (var rule in configuration.Rules.Where(r => DiscordMessageRenderer.Matches(r, evt)))
                    foreach (var destination in configuration.Destinations.Where(d => d.Enabled && rule.DestinationIds.Contains(d.Id)))
                        await DeliverAsync(destination, evt,
                            DiscordMessageRenderer.BuildPayload(evt, rule, configuration.Username), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    // Exception messages from HTTP and configuration may contain credentials.
                    _logger.LogWarning("Discord event could not be processed ({ErrorType})", ex.GetType().Name);
                    Record(new(DateTimeOffset.UtcNow, "", "Configuration", evt.EventType, evt.ServerName,
                        false, "Impossible de lire ou d'appliquer la configuration Discord."));
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public async Task<DiscordTestResult> TestAsync(DiscordTestRequest request, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var configuration = await scope.ServiceProvider.GetRequiredService<IDiscordNotificationSettings>()
            .GetAsync(ct, includeSecrets: true);
        var destination = configuration.Destinations.FirstOrDefault(d => d.Id == request.DestinationId)
            ?? throw new ArgumentException("Salon introuvable. Enregistre les réglages avant le test.");
        var definition = DiscordEvents.Catalog.FirstOrDefault(e => e.Id == request.EventType)
            ?? throw new ArgumentException("Événement inconnu.");
        if (request.ServerName is null || request.ServerName.Length > 128)
            throw new ArgumentException("Nom de serveur invalide.");
        DiscordNotificationRule? rule = null;
        if (request.RuleId is not null)
            rule = configuration.Rules.FirstOrDefault(r => r.Id == request.RuleId)
                ?? throw new ArgumentException("Règle introuvable.");
        var evt = definition.Sample(request.ServerName);
        // A manual test deliberately works even while automatic delivery is paused.
        return await DeliverAsync(destination, evt,
            DiscordMessageRenderer.BuildPayload(evt, rule, configuration.Username, isTest: true), ct);
    }

    private async Task<DiscordTestResult> DeliverAsync(DiscordDestination destination, DiscordEvent evt, string payload, CancellationToken ct)
    {
        DiscordTestResult result;
        if (!DiscordNotificationSettings.IsWebhookUrl(destination.WebhookUrl))
            result = new(false, "URL de webhook Discord invalide. Modifie ce salon.");
        else
        {
            await _sendGate.WaitAsync(ct);
            try { result = await SendAsync(destination.WebhookUrl!, payload, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                _logger.LogWarning("Discord delivery failed ({ErrorType})", ex.GetType().Name);
                result = new(false, "Discord est injoignable ou le délai d'envoi est dépassé.");
            }
            finally { _sendGate.Release(); }
        }
        Record(new(DateTimeOffset.UtcNow, destination.Id, destination.Name, evt.EventType, evt.ServerName, result.Success, result.Detail));
        return result;
    }

    private async Task<DiscordTestResult> SendAsync(string webhookUrl, string payload, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var delay = _nextSend - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, ct);
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            // wait=true makes Discord confirm message creation rather than silently dropping errors.
            using var response = await client.PostAsync(webhookUrl + "?wait=true", content, ct);
            _nextSend = DateTimeOffset.UtcNow.AddSeconds(1);
            if (response.IsSuccessStatusCode) return new(true, "Message accepté par Discord.");
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retry = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow);
                if (retry is null)
                {
                    try
                    {
                        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                        if (body.RootElement.TryGetProperty("retry_after", out var value)
                            && value.TryGetDouble(out var seconds) && double.IsFinite(seconds) && seconds >= 0 && seconds <= 86400)
                            retry = TimeSpan.FromSeconds(seconds);
                    }
                    catch (JsonException) { }
                }
                _nextSend = DateTimeOffset.UtcNow + (retry > TimeSpan.Zero ? retry.Value : TimeSpan.FromSeconds(5));
                if (attempt < 2) continue;
                return new(false, "Limite Discord atteinte après trois tentatives. Réessaie plus tard.");
            }
            if ((int)response.StatusCode >= 500 && attempt < 2)
            {
                _nextSend = DateTimeOffset.UtcNow.AddSeconds(Math.Pow(2, attempt + 1));
                continue;
            }
            return new(false, response.StatusCode switch
            {
                HttpStatusCode.NotFound => "Webhook introuvable ou supprimé. Remplace son URL.",
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Webhook refusé. Vérifie les autorisations du salon.",
                HttpStatusCode.BadRequest => "Message refusé par Discord. Vérifie son modèle.",
                _ => $"Discord a refusé le message (HTTP {(int)response.StatusCode})."
            });
        }
        return new(false, "Échec de l'envoi.");
    }

    private void Record(DiscordDeliveryEntry entry)
    {
        _journal.Enqueue(entry);
        while (_journal.Count > 100) _journal.TryDequeue(out _);
    }

    public static string BuildPayload(DiscordEvent evt) => DiscordMessageRenderer.BuildPayload(evt);
}
