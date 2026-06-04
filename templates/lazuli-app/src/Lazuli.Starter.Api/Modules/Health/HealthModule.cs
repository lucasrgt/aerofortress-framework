namespace Lazuli.Starter.Api.Modules.Health;

/// <summary>The Health module groups its slices under /health. One explicit line per slice.</summary>
public static class HealthModule
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var health = app.MapGroup("/health");
        Ping.Map(health);
    }
}
