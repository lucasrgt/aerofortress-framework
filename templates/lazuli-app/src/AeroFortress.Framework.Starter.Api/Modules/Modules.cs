using AeroFortress.Framework.Starter.Api.Modules.Health;

namespace AeroFortress.Framework.Starter.Api.Modules;

/// <summary>The module registry — the one explicit list of the app's modules, wired on both sides: AddModules
/// registers each module's services, MapModules its routes. Adding a module is a line in each (af g appends
/// them). Explicit on purpose — AeroFortress discovers nothing by reflection; the doctor (AF0016) checks every
/// [Module] appears here, so a module can't be silently left unwired.</summary>
public static class Modules
{
    public static IServiceCollection AddModules(this IServiceCollection services, IConfiguration configuration)
    {
        HealthModule.AddServices(services, configuration);
        return services;
    }

    public static void MapModules(this WebApplication app)
    {
        HealthModule.Map(app);
    }
}
