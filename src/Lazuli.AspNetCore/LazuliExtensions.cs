using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuli.AspNetCore;

/// <summary>
/// The framework's composition-root wiring in one pair of calls, so an app's <c>Program.cs</c> reads as a thin
/// index rather than re-deriving Lazuli's conventions every time. <see cref="AddLazuli"/> registers what every
/// Lazuli app shares — the slice-aware OpenAPI document (<see cref="OpenApiExtensions.AddLazuliOpenApi"/>) and
/// the enum-as-name JSON convention; <see cref="UseLazuli"/> serves the contract. Auth, persistence, and vendor
/// adapters stay the app's own calls — the framework ships the conventions, not the app's choices.
/// </summary>
public static class LazuliExtensions
{
    /// <summary>Register Lazuli's universal conventions: the slice-aware OpenAPI document and enum-as-name JSON.</summary>
    public static IServiceCollection AddLazuli(this IServiceCollection services)
    {
        services.AddLazuliOpenApi();
        // Enums cross the wire as their names ("host", not 0) — readable, and stable against reordering.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
        return services;
    }

    /// <summary>Serve Lazuli's framework endpoints — the OpenAPI document at <c>/openapi/v1.json</c>, the typed
    /// contract a client generates from.</summary>
    public static WebApplication UseLazuli(this WebApplication app)
    {
        app.MapOpenApi();
        return app;
    }
}
