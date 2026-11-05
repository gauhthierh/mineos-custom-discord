namespace MineOS.Application.Interfaces;

public interface IDiscordNotificationSettings
{
    Task<DiscordNotificationConfiguration> GetAsync(CancellationToken ct, bool includeSecrets = false);
    Task<DiscordNotificationConfiguration> SaveAsync(DiscordNotificationConfiguration configuration, CancellationToken ct);
}

public interface IDiscordNotificationDelivery
{
    DiscordDeliveryJournal GetJournal();
    Task<DiscordTestResult> TestAsync(DiscordTestRequest request, CancellationToken ct);
}

public interface IDiscordNotificationPreview
{
    string Build(DiscordPreviewRequest request);
}

public sealed record DiscordNotificationConfiguration
{
    public bool Enabled { get; init; } = true;
    public string Username { get; init; } = "MineOS";
    public List<DiscordDestination> Destinations { get; init; } = [];
    public List<DiscordNotificationRule> Rules { get; init; } = [];
}

public sealed record DiscordDestination
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Enabled { get; init; } = true;
    // Only supplied when replacing a webhook. Never returned by the admin API.
    public string? WebhookUrl { get; init; }
    public bool HasWebhook { get; init; }
}

public sealed record DiscordNotificationRule
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool Enabled { get; init; } = true;
    public List<string> EventTypes { get; init; } = [];
    public List<string> ServerNames { get; init; } = [];
    public List<string> DestinationIds { get; init; } = [];
    public string TitleTemplate { get; init; } = "{title}";
    public string MessageTemplate { get; init; } = "{message}";
    public string Format { get; init; } = "embed";
    public string? Color { get; init; }
    public bool ShowTimestamp { get; init; } = true;
    public bool ShowServer { get; init; } = true;
}

public sealed record DiscordDeliveryEntry(
    DateTimeOffset Timestamp, string DestinationId, string DestinationName,
    string EventType, string? ServerName, bool Success, string Detail);
public sealed record DiscordDeliveryJournal(IReadOnlyList<DiscordDeliveryEntry> Entries, long DroppedEvents);
public sealed record DiscordTestRequest(string DestinationId, string? RuleId = null,
    string EventType = "server.started", string ServerName = "serveur-exemple");
public sealed record DiscordTestResult(bool Success, string Detail);
public sealed record DiscordPreviewRequest(DiscordNotificationRule Rule,
    string EventType = "server.started", string ServerName = "serveur-exemple", string Username = "MineOS");

public sealed record DiscordEventDefinition(string Id, string Category, string Label,
    string SampleTitle, string SampleMessage, DiscordEventLevel Level)
{
    public DiscordEvent Sample(string? serverName) => new(SampleTitle,
        SampleMessage.Replace("{server}", serverName ?? "MineOS"), Level, serverName,
        DateTimeOffset.UtcNow, Id);
}

/// <summary>Stable identifiers; routing never depends on translated message titles.</summary>
public static class DiscordEvents
{
    public const string Started = "server.started";
    public const string Stopped = "server.stopped";
    public const string Created = "server.created";
    public const string Deleted = "server.deleted";
    public const string Crashed = "server.crashed";
    public const string Restarted = "server.auto-restarted";
    public const string RestartFailed = "server.auto-restart-failed";

    public static IReadOnlyList<DiscordEventDefinition> Catalog { get; } = BuildCatalog();

    public static string ForJob(string type, string outcome)
    {
        var category = type.Trim().ToLowerInvariant() switch
        {
            "backup" => "backup", "restore" => "restore", "import" => "import",
            "mod-install" or "modpack-install" or "modrinth-modpack-install" => "installation",
            _ => "job"
        };
        return $"{category}.{outcome}";
    }

    private static IReadOnlyList<DiscordEventDefinition> BuildCatalog()
    {
        var events = new List<DiscordEventDefinition>
        {
            new(Started, "Statut", "Serveur en ligne", "Serveur en ligne", "{server} est maintenant en ligne.", DiscordEventLevel.Success),
            new(Stopped, "Statut", "Serveur hors ligne", "Serveur hors ligne", "{server} est maintenant hors ligne.", DiscordEventLevel.Warning),
            new(Crashed, "Incidents", "Crash détecté", "Crash détecté", "{server} s'est arrêté de façon inattendue.", DiscordEventLevel.Error),
            new(Restarted, "Incidents", "Redémarrage automatique réussi", "Serveur redémarré", "{server} a été redémarré automatiquement.", DiscordEventLevel.Success),
            new(RestartFailed, "Incidents", "Redémarrage automatique échoué", "Échec du redémarrage", "Impossible de redémarrer {server}.", DiscordEventLevel.Error),
            new(Created, "Administration", "Serveur créé", "Serveur créé", "{server} a été créé.", DiscordEventLevel.Success),
            new(Deleted, "Administration", "Serveur supprimé", "Serveur supprimé", "{server} a été supprimé.", DiscordEventLevel.Warning)
        };
        foreach (var (id, category) in new[] { ("backup", "Sauvegardes"), ("restore", "Restaurations"),
                     ("import", "Imports"), ("installation", "Installations"), ("job", "Autres tâches") })
        {
            if (id is "backup" or "restore")
                events.Add(new($"{id}.started", category, "Début", $"{category} : début", "Opération démarrée pour {server}.", DiscordEventLevel.Info));
            events.Add(new($"{id}.completed", category, "Réussite", $"{category} : réussite", "Opération terminée pour {server}.", DiscordEventLevel.Success));
            events.Add(new($"{id}.failed", category, "Échec", $"{category} : échec", "Échec de l'opération pour {server}.", DiscordEventLevel.Error));
            events.Add(new($"{id}.cancelled", category, "Annulation", $"{category} : annulation", "Opération annulée pour {server}.", DiscordEventLevel.Warning));
        }
        events.Add(new("system.notification", "Autres", "Autres notifications", "Notification MineOS", "Nouvelle notification pour {server}.", DiscordEventLevel.Info));
        return events.AsReadOnly();
    }
}
