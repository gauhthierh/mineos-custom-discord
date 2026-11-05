using MineOS.Application.Interfaces;

namespace MineOS.Api.Endpoints;

public static class DiscordNotificationEndpoints
{
    public static RouteGroupBuilder MapDiscordNotificationEndpoints(this RouteGroupBuilder api)
    {
        var discord = api.MapGroup("/discord").RequireAuthorization(policy => policy.RequireRole("admin"));
        discord.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            try { return await next(context); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        discord.MapGet("/events", () => Results.Ok(DiscordEvents.Catalog));
        discord.MapGet("/configuration", async (IDiscordNotificationSettings settings, CancellationToken ct) =>
            Results.Ok(await settings.GetAsync(ct)));
        discord.MapPut("/configuration", async (DiscordNotificationConfiguration configuration,
            IDiscordNotificationSettings settings, CancellationToken ct) => Results.Ok(await settings.SaveAsync(configuration, ct)));
        discord.MapGet("/journal", (IDiscordNotificationDelivery delivery) => Results.Ok(delivery.GetJournal()));
        discord.MapPost("/test", async (DiscordTestRequest request, IDiscordNotificationDelivery delivery, CancellationToken ct) =>
            Results.Ok(await delivery.TestAsync(request, ct)));
        discord.MapPost("/preview", (DiscordPreviewRequest request, IDiscordNotificationPreview preview) =>
            Results.Content(preview.Build(request), "application/json"));
        return api;
    }
}
