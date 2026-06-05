using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuli.AspNetCore;

/// <summary>
/// Lazuli's OpenAPI wiring — the slice-aware document conventions, owned by the framework so an app never
/// re-derives them in its composition root. Pairs with the typed <see cref="ResultHttpExtensions.ToHttp{T}"/>
/// (which makes each endpoint's 200/error schema inferable): together they make the OpenAPI document a faithful,
/// typed projection of the slices, from which a typed client generates — the foundation of the "wired, not
/// mocked" frontend harness.
/// </summary>
public static class OpenApiExtensions
{
    /// <summary>Add the OpenAPI document with Lazuli's conventions. The one that matters: a slice nests its own
    /// <c>Input</c>/<c>Output</c> record, so every slice's <c>Output</c> shares the short name <c>Output</c>;
    /// left to the default, they collide onto one schema and the generated client is mistyped. This qualifies a
    /// nested record by its declaring slice (<c>LegalDocCurrentOutput</c>, <c>SubmitReviewOutput</c>), so each
    /// slice's contract is a distinct schema — pulled from the <c>[Slice]</c> shape, not hand-listed per app.</summary>
    /// <example>
    /// In the composition root, replace the bare <c>AddOpenApi()</c>:
    /// <code>
    /// builder.Services.AddLazuliOpenApi();
    /// // ... and, after build:
    /// app.MapOpenApi();   // serves /openapi/v1.json
    /// </code>
    /// </example>
    public static IServiceCollection AddLazuliOpenApi(this IServiceCollection services) =>
        services.AddOpenApi(options =>
            options.CreateSchemaReferenceId = type =>
                type.Type.DeclaringType is { } slice
                    ? slice.Name + type.Type.Name
                    : OpenApiOptions.CreateDefaultSchemaReferenceId(type));
}
