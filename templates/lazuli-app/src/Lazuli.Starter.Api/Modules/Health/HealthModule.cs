namespace Lazuli.Starter.Api.Modules.Health;

/// <summary>The Health module's wiring root — it owns both halves of its composition: <see cref="AddServices"/>
/// (its own DI) and <see cref="Map"/> (its routes, under /health, one line per slice). The module registry calls
/// both; the doctor (LZ0015/LZ0016) checks the shape and that it is registered.</summary>
[Module]
public static class HealthModule
{
    /// <summary>The module's own service registration — none yet, but the seam is declared uniformly, the way
    /// every module carries both halves of its wiring (this and Map).</summary>
    public static IServiceCollection AddServices(IServiceCollection services, IConfiguration configuration) =>
        services;

    public static void Map(IEndpointRouteBuilder app)
    {
        var health = app.MapGroup("/health");
        Ping.Map(health);
    }
}
