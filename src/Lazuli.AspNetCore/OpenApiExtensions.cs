using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

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
        {
            options.CreateSchemaReferenceId = type =>
                type.Type.DeclaringType is { } slice
                    ? slice.Name + type.Type.Name
                    : OpenApiOptions.CreateDefaultSchemaReferenceId(type);

            // Enumerate the error codes (every *ErrorCodes registry constant, gathered by reflection) into the
            // ErrorBody.Code schema, so the generated client is typed on the closed set and the frontend can be
            // checked for an exhaustive translation of each. LZ0018 guarantees every code is such a constant, so
            // this set is the whole contract.
            options.AddSchemaTransformer((schema, context, _) =>
            {
                if (context.JsonTypeInfo.Type == typeof(ErrorBody)
                    && schema.Properties is { } properties
                    && properties.TryGetValue("code", out var codeSchema)
                    && codeSchema is OpenApiSchema concreteCode)
                {
                    var codes = ErrorCodes();
                    if (codes.Count > 0)
                        concreteCode.Enum = codes.Select(code => (JsonNode)JsonValue.Create(code)!).ToList();
                }
                return Task.CompletedTask;
            });

            // Mirror scalar value objects in the contract. A [ValueObject] carrying a ScalarJsonConverter
            // serializes as the primitive it wraps, but the document generator only sees the CLR type — left
            // alone it emits a bare {} schema and the generated client mistypes every amount and slug. This
            // reads the converter's TPrimitive off the attribute and writes that primitive's schema instead,
            // so the wire transparency holds end-to-end with no per-type transformer in the app.
            options.AddSchemaTransformer((schema, context, _) =>
            {
                if (ScalarPrimitiveOf(context.JsonTypeInfo.Type) is { } primitive)
                {
                    var (type, format) = SchemaFor(primitive);
                    schema.Type = type;
                    schema.Format = format;
                    schema.Properties = null;
                }
                return Task.CompletedTask;
            });
        });

    // The TPrimitive of the ScalarJsonConverter<TVo, TPrimitive> subclass the type's [JsonConverter] points at,
    // or null when the type carries no such converter.
    private static Type? ScalarPrimitiveOf(Type type)
    {
        var converter = type.GetCustomAttribute<System.Text.Json.Serialization.JsonConverterAttribute>()?.ConverterType;
        for (var t = converter; t is not null; t = t.BaseType)
            if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Abstractions.ScalarJsonConverter<,>))
                return t.GetGenericArguments()[1];
        return null;
    }

    // The OpenAPI (type, format) a wire primitive maps to. Unknown primitives fall back to string — the safe
    // projection for anything a custom converter writes as text.
    private static (JsonSchemaType Type, string? Format) SchemaFor(Type primitive) =>
        Type.GetTypeCode(primitive) switch
        {
            TypeCode.Int64 or TypeCode.UInt64 => (JsonSchemaType.Integer, "int64"),
            TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int16 or TypeCode.UInt16 or TypeCode.Byte or TypeCode.SByte =>
                (JsonSchemaType.Integer, "int32"),
            TypeCode.Decimal or TypeCode.Double => (JsonSchemaType.Number, "double"),
            TypeCode.Single => (JsonSchemaType.Number, "float"),
            TypeCode.Boolean => (JsonSchemaType.Boolean, null),
            TypeCode.DateTime => (JsonSchemaType.String, "date-time"),
            _ when primitive == typeof(Guid) => (JsonSchemaType.String, "uuid"),
            _ when primitive == typeof(DateOnly) => (JsonSchemaType.String, "date"),
            _ when primitive == typeof(DateTimeOffset) => (JsonSchemaType.String, "date-time"),
            _ => (JsonSchemaType.String, null),
        };

    // Every error code in the app: the public string constants on classes named *ErrorCodes, across the loaded
    // assemblies. LZ0018 enforces that this registry set is the complete set of codes the app can emit.
    private static IReadOnlyList<string> ErrorCodes() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => !assembly.IsDynamic)
            .SelectMany(SafeTypes)
            .Where(type => type is { IsClass: true, IsAbstract: true, IsSealed: true }
                && type.Name.EndsWith("ErrorCodes", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => field.GetRawConstantValue() as string)
            .Where(code => !string.IsNullOrEmpty(code))
            .Select(code => code!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<Type> SafeTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(type => type is not null)!; }
    }
}
