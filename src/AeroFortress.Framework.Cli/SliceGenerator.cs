using System.Linq;

namespace AeroFortress.Framework.Cli;

/// <summary>
/// Generates a conformant slice and its co-located tests — code that passes the doctor by
/// construction (LZ0001's shape, LZ0003's test, LZ0012's endpoint name, LZ0018's registry constant,
/// and for a critical slice LZ0008's journeys), so the
/// convention is born right instead of the author having to remember it. The root namespace is read
/// from the project's .csproj in the target directory; the test namespace follows the
/// <c>&lt;App&gt;.Api → &lt;App&gt;.Tests</c> convention.
/// </summary>
public static class SliceGenerator
{
    /// <summary>Generate <paramref name="name"/> in <paramref name="module"/> under the <paramref name="root"/> project.</summary>
    /// <param name="root">The application project directory (holding the .csproj).</param>
    /// <param name="module">The module the slice belongs to.</param>
    /// <param name="name">The slice (operation) name.</param>
    /// <param name="critical">When true, mark the slice <c>[Critical]</c> and scaffold happy + sad journeys.</param>
    public static int Generate(string root, string module, string name, bool critical = false)
    {
        var csproj = Directory.GetFiles(root, "*.csproj").FirstOrDefault();
        if (csproj is null)
        {
            Console.Error.WriteLine("lazuli: no .csproj here — run this from the application project directory.");
            return 1;
        }

        var appNamespace = Path.GetFileNameWithoutExtension(csproj);
        var testNamespace = appNamespace.EndsWith(".Api", StringComparison.Ordinal)
            ? appNamespace[..^4] + ".Tests"
            : appNamespace + ".Tests";

        var directory = Path.Combine(root, "Modules", module, "Slices");
        var slicePath = Path.Combine(directory, name + ".cs");
        var testPath = Path.Combine(directory, name + ".Tests.cs");

        if (File.Exists(slicePath))
        {
            Console.Error.WriteLine($"lazuli: {slicePath} already exists.");
            return 1;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(slicePath, Slice(appNamespace, module, name, critical));
        File.WriteAllText(testPath, Test(testNamespace, module, name));
        Console.WriteLine($"created {slicePath}");
        Console.WriteLine($"created {testPath}");

        // The scaffolded validation references a registry constant (LZ0018) — ensure the module's registry has it.
        ErrorCodeScaffold.EnsureModuleCode(Path.Combine(root, "Modules", module), appNamespace, module,
            "IdRequired", "id.required", "The id input is required.");

        if (critical)
        {
            var journeys = Path.Combine(root, "Journeys");
            Directory.CreateDirectory(journeys);
            var journeyPath = Path.Combine(journeys, name + "Journey.Tests.cs");
            File.WriteAllText(journeyPath, Journey(appNamespace, testNamespace, module, name));
            Console.WriteLine($"created {journeyPath}");
        }

        return 0;
    }

    private static string Slice(string appNamespace, string module, string name, bool critical)
    {
        var attributes = critical ? "[Slice]\n[Critical]" : "[Slice]";
        return $$"""
            namespace {{appNamespace}}.Modules.{{module}};

            /// <summary>{{name}} — fill in the operation; the "why" lives here until it outgrows a header.</summary>
            {{attributes}}
            public static class {{name}}
            {
                public record Input(Guid Id);
                public record Output(Guid Id);

                public static Task<Result<Output>> Handle(Input input, CancellationToken ct)
                {
                    var validation = new Validation()
                        .Require(input.Id, "id", {{module}}ErrorCodes.IdRequired);
                    if (validation.Failed)
                        return Task.FromResult<Result<Output>>(validation.ToError());

                    return Task.FromResult<Result<Output>>(new Output(input.Id));
                }

                public static void Map(IEndpointRouteBuilder app) =>
                    app.MapPost("/{{name.ToLowerInvariant()}}", async (Input input, CancellationToken ct) =>
                            (await Handle(input, ct)).ToHttp())
                        .WithName(nameof({{name}}));
            }

            """;
    }

    private static string Test(string testNamespace, string module, string name) => $$"""
        using {{testNamespace.Replace(".Tests", ".Api")}}.Modules.{{module}};

        namespace {{testNamespace}}.Modules.{{module}};

        public class {{name}}Tests
        {
            [Unit]
            [Fact]
            public async Task {{name}}_succeeds()
            {
                var id = Guid.NewGuid();

                var result = await {{name}}.Handle(new {{name}}.Input(id), default);

                Assert.True(result.IsSuccess);
                Assert.Equal(id, result.Value.Id);
            }

            [Unit]
            [Fact]
            public async Task {{name}}_rejects_an_empty_id()
            {
                var result = await {{name}}.Handle(new {{name}}.Input(Guid.Empty), default);

                Assert.True(result.IsFailure);
                Assert.Equal(ErrorKind.Validation, result.Error.Kind);
            }
        }

        """;

    private static string Journey(string appNamespace, string testNamespace, string module, string name) => $$"""
        using {{appNamespace}}.Modules.{{module}};

        namespace {{testNamespace}}.Journeys;

        // Generated for the [Critical] {{name}} slice. Implement both journeys, then remove the Skip:
        //   happy — prove the success effect is observable end-to-end.
        //   sad   — a rejected request returns the failure status AND leaves no state changed.
        public class {{name}}Journey
        {
            [E2E]
            [Journey(typeof({{name}}), JourneyPath.Happy)]
            [Fact(Skip = "implement the happy journey")]
            public Task {{name}}_happy_path() => Task.CompletedTask;

            [E2E]
            [Journey(typeof({{name}}), JourneyPath.Sad)]
            [Fact(Skip = "implement the sad journey")]
            public Task {{name}}_rejected_changes_nothing() => Task.CompletedTask;
        }

        """;
}
