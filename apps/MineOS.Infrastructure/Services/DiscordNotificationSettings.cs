using System.Text.Json;
using System.Text.RegularExpressions;
using MineOS.Application.Interfaces;

namespace MineOS.Infrastructure.Services;

public sealed partial class DiscordNotificationSettings(ISettingsService settings) : IDiscordNotificationSettings
{
    public const string SettingKey = "Discord:Notifications";
    private static readonly SemaphoreSlim SaveGate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DiscordNotificationConfiguration> GetAsync(CancellationToken ct, bool includeSecrets = false)
    {
        var raw = await settings.GetAsync(SettingKey, ct);
        DiscordNotificationConfiguration configuration;
        if (raw is not null)
        {
            // Fail closed: corrupted custom settings must never restore the legacy catch-all.
            configuration = JsonSerializer.Deserialize<DiscordNotificationConfiguration>(raw, JsonOptions)
                ?? throw new InvalidOperationException("Configuration Discord invalide.");
        }
        else
        {
            var legacy = await settings.GetAsync(SettingsService.Keys.DiscordWebhookUrl, ct);
            configuration = new DiscordNotificationConfiguration();
            if (!string.IsNullOrWhiteSpace(legacy))
            {
                configuration = configuration with
                {
                    Destinations = [new() { Id = "legacy", Name = "Salon existant", WebhookUrl = legacy }],
                    Rules = [new() { Id = "legacy", Name = "Notifications existantes",
                        EventTypes = DiscordEvents.Catalog.Select(e => e.Id).ToList(), DestinationIds = ["legacy"] }]
                };
            }
        }
        return includeSecrets ? configuration : Redact(configuration);
    }

    public async Task<DiscordNotificationConfiguration> SaveAsync(DiscordNotificationConfiguration configuration, CancellationToken ct)
    {
        await SaveGate.WaitAsync(ct);
        try
        {
            ValidateStructure(configuration);
            var previous = await GetAsync(ct, includeSecrets: true);
            var destinations = configuration.Destinations.Select(destination =>
            {
                var url = string.IsNullOrWhiteSpace(destination.WebhookUrl)
                    ? previous.Destinations.FirstOrDefault(d => d.Id == destination.Id)?.WebhookUrl
                    : destination.WebhookUrl.Trim();
                if (!IsWebhookUrl(url))
                    throw new ArgumentException($"Le salon « {destination.Name} » nécessite une URL de webhook Discord HTTPS valide.");
                return destination with { Name = destination.Name.Trim(), WebhookUrl = url, HasWebhook = true };
            }).ToList();
            configuration = configuration with { Username = configuration.Username.Trim(), Destinations = destinations };
            await settings.SetAsync(SettingKey, JsonSerializer.Serialize(configuration, JsonOptions), ct);
            return Redact(configuration);
        }
        finally { SaveGate.Release(); }
    }

    private static DiscordNotificationConfiguration Redact(DiscordNotificationConfiguration configuration) =>
        configuration with { Destinations = configuration.Destinations.Select(d => d with
            { HasWebhook = !string.IsNullOrWhiteSpace(d.WebhookUrl), WebhookUrl = null }).ToList() };

    public static bool IsWebhookUrl(string? value) =>
        value is { Length: <= 512 } && !value.Contains('%') && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.IsDefaultPort && uri.UserInfo.Length == 0
        && uri.Query.Length == 0 && uri.Fragment.Length == 0
        && uri.Host is "discord.com" or "discordapp.com" or "canary.discord.com" or "ptb.discord.com"
        && WebhookPath().IsMatch(uri.AbsolutePath);

    public static void ValidateStructure(DiscordNotificationConfiguration configuration)
    {
        if (configuration.Destinations is null || configuration.Rules is null
            || configuration.Destinations.Count > 20 || configuration.Rules.Count > 60)
            throw new ArgumentException("Maximum : 20 salons et 60 règles.");
        if (string.IsNullOrWhiteSpace(configuration.Username) || configuration.Username.Length > 80)
            throw new ArgumentException("Le nom de l'expéditeur doit contenir entre 1 et 80 caractères.");
        var destinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var destination in configuration.Destinations)
        {
            if (destination is null || !ValidId(destination.Id) || !destinations.Add(destination.Id)
                || string.IsNullOrWhiteSpace(destination.Name) || destination.Name.Length > 80)
                throw new ArgumentException("Chaque salon doit avoir un identifiant unique et un nom de 1 à 80 caractères.");
        }
        var rules = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in configuration.Rules)
        {
            ValidateRule(rule);
            if (!rules.Add(rule.Id) || rule.DestinationIds.Any(id => !destinations.Contains(id)))
                throw new ArgumentException("Une règle contient un identifiant en double ou un salon inexistant.");
        }
    }

    public static void ValidateRule(DiscordNotificationRule? rule)
    {
        if (rule is null || !ValidId(rule.Id) || string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Length > 100
            || rule.EventTypes is null || rule.ServerNames is null || rule.DestinationIds is null)
            throw new ArgumentException("Règle Discord incomplète.");
        if (rule.EventTypes.Count is 0 or > 50 || rule.EventTypes.Any(id => !DiscordEvents.Catalog.Any(e => e.Id == id)))
            throw new ArgumentException("Choisis au moins un événement reconnu pour chaque règle.");
        if (rule.DestinationIds.Count is 0 or > 20 || rule.DestinationIds.Distinct().Count() != rule.DestinationIds.Count)
            throw new ArgumentException("Choisis au moins un salon, sans doublon, pour chaque règle.");
        if (rule.ServerNames.Count > 100 || rule.ServerNames.Any(s => string.IsNullOrWhiteSpace(s) || s.Length > 128))
            throw new ArgumentException("Le filtre des serveurs est invalide.");
        if (rule.Format is not ("embed" or "text") || (rule.Color is not null && !HexColor().IsMatch(rule.Color)))
            throw new ArgumentException("Le format ou la couleur du message est invalide.");
        ValidateTemplate(rule.TitleTemplate, 256);
        ValidateTemplate(rule.MessageTemplate, 4000);
    }

    private static void ValidateTemplate(string? template, int max)
    {
        if (string.IsNullOrWhiteSpace(template) || template.Length > max)
            throw new ArgumentException($"Les modèles doivent contenir entre 1 et {max} caractères.");
        var remaining = template;
        foreach (var token in new[] { "title", "message", "server", "event", "level", "timestamp" })
            remaining = remaining.Replace("{" + token + "}", "");
        if (remaining.Contains('{') || remaining.Contains('}'))
            throw new ArgumentException("Variable inconnue. Variables disponibles : {title}, {message}, {server}, {event}, {level}, {timestamp}.");
    }

    private static bool ValidId(string? id) => id is { Length: > 0 and <= 64 } && Identifier().IsMatch(id);
    [GeneratedRegex(@"^/api(?:/v[0-9]+)?/webhooks/[0-9]+/[A-Za-z0-9_-]+$")]
    private static partial Regex WebhookPath();
    [GeneratedRegex(@"^[A-Za-z0-9_-]+$")]
    private static partial Regex Identifier();
    [GeneratedRegex(@"^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColor();
}
