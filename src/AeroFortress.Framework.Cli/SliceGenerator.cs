using System.Linq;

namespace AeroFortress.Framework.Cli;

/// <summary>
/// Generates a conformant slice and its co-located tests — code that passes the doctor by
/// construction (AF0001's shape, AF0003's test, AF0012's endpoint name, AF0018's registry constant,
/// and for a critical slice AF0008's journeys), so the
/// convention is born right instead of the author having to remember it. The root namespace is read
/// from the project's .csproj in the target directory; the test namespace follows the
/// <c>&lt;App&gt;.Api → &lt;App&gt;.Tests</c> convention.
/// </summary>
public static class SliceGenerator
{
    /// <summary>
    /// The <c>af g slice</c> entry: parse the flags (<c>--critical</c>, <c>--verify id,id</c>) and generate
    /// from the current directory. Kept beside the generator so <c>Program</c> stays an index.
    /// </summary>
    /// <param name="module">The module the slice belongs to.</param>
    /// <param name="name">The slice (operation) name.</param>
    /// <param name="flags">The remaining command-line flags.</param>
    public static int Run(string module, string name, string[] flags)
    {
        var critical = false;
        var verify = new List<string>();
        for (var i = 0; i < flags.Length; i++)
        {
            switch (flags[i])
            {
                case "--critical":
                    critical = true;
                    break;
                case "--verify" when i + 1 < flags.Length:
                    verify.AddRange(flags[++i].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
                    break;
                case "--verify":
                    Console.Error.WriteLine("af: --verify expects a comma-separated list of AVP criterion ids (see `af criteria list`).");
                    return 1;
                default:
                    Console.Error.WriteLine($"af: unknown flag '{flags[i]}' for `g slice` (expected --critical and/or --verify <id,id>).");
                    return 1;
            }
        }

        if (verify.Count == 0)
        {
            Console.Error.WriteLine("af: every slice needs at least one AVP criterion; pass --verify <id,id> "
                                  + "(use `af criteria suggest <words...>` to choose). ");
            return 1;
        }

        return Generate(Directory.GetCurrentDirectory(), module, name, critical, verify);
    }

    /// <summary>Generate <paramref name="name"/> in <paramref name="module"/> under the <paramref name="root"/> project.</summary>
    /// <param name="root">The application project directory (holding the .csproj).</param>
    /// <param name="module">The module the slice belongs to.</param>
    /// <param name="name">The slice (operation) name.</param>
    /// <param name="critical">When true, mark the slice <c>[Critical]</c> and scaffold happy + sad journeys.</param>
    /// <param name="verify">
    /// AVP criterion ids to declare for the slice — correct-by-construction: the module's spec manifest gains
    /// the declaration and a co-located, red-by-design <c>[AVP]</c> proof scaffold is emitted with it, so the
    /// AF0030/AF0031 bridge is born closed instead of caught later.
    /// </param>
    public static int Generate(
        string root, string module, string name, bool critical = false, IReadOnlyList<string>? verify = null)
    {
        var csproj = Directory.GetFiles(root, "*.csproj").FirstOrDefault();
        if (csproj is null)
        {
            Console.Error.WriteLine("af: no .csproj here — run this from the application project directory.");
            return 1;
        }

        if (verify is not { Count: > 0 })
        {
            Console.Error.WriteLine("af: every slice needs at least one AVP criterion; pass --verify <id,id>.");
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
            Console.Error.WriteLine($"af: {slicePath} already exists.");
            return 1;
        }

        Directory.CreateDirectory(directory);
        File.WriteAllText(slicePath, Slice(appNamespace, module, name, critical));
        File.WriteAllText(testPath, Test(testNamespace, module, name));
        Console.WriteLine($"created {slicePath}");
        Console.WriteLine($"created {testPath}");

        // The scaffolded validation references a registry constant (AF0018) — ensure the module's registry has it.
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

        DeclareAndProve(root, directory, testNamespace, appNamespace, module, name, verify);

        return 0;
    }

    // The correct-by-construction leg: declare the criteria in the module's spec manifest and scaffold the
    // co-located, red-by-design proof file — obligation and proof in the same change-set, always.
    private static void DeclareAndProve(
        string root, string sliceDir, string testNamespace, string appNamespace,
        string module, string name, IReadOnlyList<string> verify)
    {
        var manifest = SpecManifestScaffold.EnsureDeclared(Path.Combine(root, "Modules", module), module, name, verify);
        Console.WriteLine($"declared {name} ({string.Join(", ", verify)}) in {manifest}");

        var proofPath = Path.Combine(sliceDir, name + ".Avp.Tests.cs");
        if (File.Exists(proofPath))
        {
            Console.Error.WriteLine($"af: {proofPath} already exists — declaration updated, proof scaffold left untouched.");
            return;
        }

        File.WriteAllText(proofPath, AvpProofScaffold.Render(testNamespace, appNamespace, module, name, verify));
        Console.WriteLine($"created {proofPath} (red by design — bind the real endpoint to turn it green)");

        var known = new HashSet<string>(
            Assay.Net.Catalog.LoadDefault().Archetypes.SelectMany(a => a.Criteria).Select(c => c.Id),
            StringComparer.Ordinal);
        foreach (var id in verify.Where(id => !known.Contains(id)))
            Console.WriteLine($"note: '{id}' is not in the AVP catalog — treated as an off-catalog criterion "
                            + "(ADR 0002); its proof scaffold is a red placeholder until you write the verifier.");
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
