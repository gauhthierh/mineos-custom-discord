using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using MineOS.Application.Interfaces;

namespace MineOS.Infrastructure.Services;

public sealed class DiscordNotificationPreview : IDiscordNotificationPreview
{
    public string Build(DiscordPreviewRequest request)
    {
        DiscordNotificationSettings.ValidateRule(request.Rule);
        if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length > 80
            || request.ServerName is null || request.ServerName.Length > 128)
            throw new ArgumentException("Nom d'expéditeur ou de serveur invalide.");
        var definition = DiscordEvents.Catalog.FirstOrDefault(e => e.Id == request.EventType)
            ?? throw new ArgumentException("Événement inconnu.");
        return DiscordMessageRenderer.BuildPayload(definition.Sample(request.ServerName), request.Rule, request.Username);
    }
}

public static partial class DiscordMessageRenderer
{
    public static bool Matches(DiscordNotificationRule rule, DiscordEvent evt) => rule.Enabled
        && rule.EventTypes.Contains(evt.EventType, StringComparer.Ordinal)
        && (rule.ServerNames.Count == 0 || (evt.ServerName is not null
            && rule.ServerNames.Contains(evt.ServerName, StringComparer.Ordinal)));

    public static string BuildPayload(DiscordEvent evt, DiscordNotificationRule? rule = null, string username = "MineOS", bool isTest = false)
    {
        rule ??= new();
        var title = Render(rule.TitleTemplate, evt);
        var message = Render(rule.MessageTemplate, evt);
        var payload = new Dictionary<string, object?>
        {
            ["username"] = Clip(username, 80),
            // Templates and server names can contain mentions. Never ping users or roles.
            ["allowed_mentions"] = new { parse = Array.Empty<string>() }
        };
        if (rule.Format == "text")
            payload["content"] = Clip($"{(isTest ? "🧪 TEST — " : "")}{title}\n{message}", 2000);
        else
        {
            var embed = new Dictionary<string, object?>
            {
                ["title"] = Clip(title, 256), ["description"] = Clip(message, 4096),
                ["color"] = rule.Color is null ? ColorFor(evt.Level)
                    : int.Parse(rule.Color[1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                ["footer"] = new { text = "MineOS" }
            };
            if (rule.ShowTimestamp) embed["timestamp"] = evt.Timestamp.UtcDateTime.ToString("o");
            if (rule.ShowServer && evt.ServerName is { Length: > 0 })
                embed["fields"] = new[] { new { name = "Server", value = Clip(evt.ServerName, 1024), inline = true } };
            payload["embeds"] = new[] { embed };
            if (isTest) payload["content"] = "🧪 Test de notification — ceci est un exemple, pas un événement réel.";
        }
        return JsonSerializer.Serialize(payload);
    }

    // Replace once: braces contained in runtime data must not become template instructions.
    public static string Render(string template, DiscordEvent evt) => Variables().Replace(template, match => match.Value switch
    {
        "{title}" => evt.Title, "{message}" => evt.Message, "{server}" => evt.ServerName ?? "MineOS",
        "{event}" => evt.EventType, "{level}" => evt.Level.ToString().ToLowerInvariant(),
        "{timestamp}" => evt.Timestamp.UtcDateTime.ToString("u"), _ => match.Value
    });

    private static string Clip(string value, int length)
    {
        if (value.Length <= length) return value;
        // Avoid splitting a UTF-16 surrogate pair (e.g. an emoji).
        var end = char.IsHighSurrogate(value[length - 1]) ? length - 1 : length;
        return value[..end];
    }

    private static int ColorFor(DiscordEventLevel level) => level switch
    {
        DiscordEventLevel.Success => 0x6AB04C, DiscordEventLevel.Warning => 0xF59E0B,
        DiscordEventLevel.Error => 0xEF4444, _ => 0x5B9EFF
    };

    [GeneratedRegex(@"\{(?:title|message|server|event|level|timestamp)\}")]
    private static partial Regex Variables();
}
