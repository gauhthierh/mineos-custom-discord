using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using MineOS.Application.Interfaces;
using MineOS.Tests.Unit;

namespace MineOS.Tests.Integration;

public class DiscordNotificationEndpointTests : IClassFixture<MineOsWebApplicationFactory>
{
    private readonly MineOsWebApplicationFactory _factory;
    public DiscordNotificationEndpointTests(MineOsWebApplicationFactory factory) => _factory = factory;

    private HttpClient AdminClient()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "dev-static-api-key-change-me");
        return client;
    }

    [Theory]
    [InlineData("configuration")]
    [InlineData("events")]
    [InlineData("journal")]
    public async Task ReadEndpoints_RequireAuthenticationAndAdministrator(string path)
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/v1/discord/{path}")).StatusCode);
        using var viewer = _factory.CreateClient();
        viewer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ViewerToken());
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/v1/discord/{path}")).StatusCode);
    }

    [Theory]
    [InlineData("configuration", "PUT")]
    [InlineData("preview", "POST")]
    [InlineData("test", "POST")]
    public async Task WriteEndpoints_RejectNonAdministrators(string path, string method)
    {
        using var viewer = _factory.CreateClient();
        viewer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", ViewerToken());
        using var request = new HttpRequestMessage(new HttpMethod(method), $"/api/v1/discord/{path}")
            { Content = JsonContent.Create(new { }) };
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task SaveAndRead_RedactWebhookAndPreserveItOnUpdate()
    {
        using var client = AdminClient();
        var response = await client.PutAsJsonAsync("/api/v1/discord/configuration", DiscordNotificationSettingsTests.Configuration());
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("secret-token", text);
        var saved = await response.Content.ReadFromJsonAsync<DiscordNotificationConfiguration>();
        Assert.True(saved!.Destinations[0].HasWebhook);
        Assert.Null(saved.Destinations[0].WebhookUrl);
        saved.Rules[0] = saved.Rules[0] with { Enabled = false };
        (await client.PutAsJsonAsync("/api/v1/discord/configuration", saved)).EnsureSuccessStatusCode();
        var get = await client.GetAsync("/api/v1/discord/configuration");
        Assert.Equal("no-store", get.Headers.CacheControl!.ToString());
        Assert.DoesNotContain("secret-token", await get.Content.ReadAsStringAsync());
        var loaded = await get.Content.ReadFromJsonAsync<DiscordNotificationConfiguration>();
        Assert.False(loaded!.Rules[0].Enabled);
        Assert.True(loaded.Destinations[0].HasWebhook);
    }

    [Fact]
    public async Task InvalidReferencesAndUnsafeWebhooks_ReturnBadRequest()
    {
        using var client = AdminClient();
        var config = DiscordNotificationSettingsTests.Configuration();
        config.Destinations[0] = config.Destinations[0] with { WebhookUrl = "https://127.0.0.1/private" };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/discord/configuration", config)).StatusCode);
        config = DiscordNotificationSettingsTests.Configuration();
        config.Rules[0].DestinationIds.Add("missing");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/discord/configuration", config)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/v1/discord/configuration", config with { Destinations = null! })).StatusCode);
    }

    [Fact]
    public async Task Preview_RendersUnsavedTemplateWithoutSending()
    {
        using var client = AdminClient();
        var rule = DiscordNotificationSettingsTests.Configuration().Rules[0] with { TitleTemplate = "Statut de {server}", MessageTemplate = "En ligne", Format = "text" };
        var response = await client.PostAsJsonAsync("/api/v1/discord/preview", new DiscordPreviewRequest(rule, ServerName: "survie"));
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Statut de survie\nEn ligne", json.GetProperty("content").GetString());
        Assert.Equal(0, json.GetProperty("allowed_mentions").GetProperty("parse").GetArrayLength());
    }

    [Fact]
    public async Task GenericSettingsEndpoint_CannotBypassDiscordValidation()
    {
        using var client = AdminClient();
        var response = await client.PutAsJsonAsync("/api/v1/settings/Discord%3ANotifications", new { value = "{}" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        response = await client.PutAsJsonAsync("/api/v1/settings/Discord%3AWebhookUrl", new { value = "http://localhost/private" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string ViewerToken()
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            "test-signing-key-at-least-32-characters-long!!")), SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken("MineOS.Test", "MineOS.Test",
            [new(ClaimTypes.NameIdentifier, "2"), new(ClaimTypes.Role, "viewer")],
            expires: DateTime.UtcNow.AddHours(1), signingCredentials: credentials));
    }
}
