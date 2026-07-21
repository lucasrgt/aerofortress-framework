namespace AeroFortress.Framework.Cli;

/// <summary>The executable verification legs for one frontend package.</summary>
/// <param name="Client">The client directory name.</param>
/// <param name="Role">The manifest role that decides whether E2E applies.</param>
/// <param name="Tests">The unit/integration test script exit code.</param>
/// <param name="Avp">The Assay/AVP verification script exit code.</param>
/// <param name="FeatureE2e">The workspace feature-to-executable-flow coverage exit code.</param>
/// <param name="E2eShape">The E2E contract exit code, or null for a shared core.</param>
/// <param name="E2e">The real E2E runner exit code, or null for a shared core.</param>
internal sealed record FrontendGateLeg(
    string Client,
    FrontendPackageRole Role,
    int Tests,
    int Avp,
    int FeatureE2e,
    int? E2eShape,
    int? E2e)
{
    /// <summary>Whether every frontend verification leg ran successfully.</summary>
    public bool Green => Tests == 0 && Avp == 0 && FeatureE2e == 0
        && (Role == FrontendPackageRole.Core || (E2eShape == 0 && E2e == 0));
}

/// <summary>Runs every frontend proof suite. Missing scripts/tools fail naturally; nothing is optional.</summary>
internal static class FrontendGate
{
    private const string AssaySuiteGlob = "**/*.assay.test.*";

    /// <summary>Run tests + Assay for every package and the two E2E legs for executable surfaces.</summary>
    public static IReadOnlyList<FrontendGateLeg> Run(string workspace, IEnumerable<FrontendPackage> clients)
    {
        var targets = clients.ToList();
        var featureCoverage = FeatureCoverage(workspace, targets);
        var legs = new List<FrontendGateLeg>();
        foreach (var target in targets)
        {
            var client = target.Path;
            var name = Path.GetFileName(client);
            Console.WriteLine($"af gate — frontend tests ({name}, non-Assay partition)...");
            // Assay suites are ordinary Vitest files, so an unfiltered test run followed by `assay verify`
            // executes them twice. Partition by filename: every non-Assay test runs here, while every Assay
            // proof runs exactly once through the acceptance verifier below.
            var tests = FrontendScriptContract.Run(client, "test", "--", $"--exclude={AssaySuiteGlob}");

            var avp = RunAssay(client, name);

            int? e2eShape = null;
            int? e2e = null;
            if (target.Role == FrontendPackageRole.Surface)
            {
                Console.WriteLine($"af gate — frontend E2E contract ({name})...");
                var manifestShape = Tooling.Run("npx", ["--no-install", "affe-e2e-doctor", "."], client);
                e2eShape = manifestShape;

                Console.WriteLine($"af gate — frontend E2E execution ({name})...");
                e2e = FrontendScriptContract.Run(client, "test:e2e");
            }

            legs.Add(new FrontendGateLeg(name, target.Role, tests, avp, featureCoverage, e2eShape, e2e));
        }

        return legs;
    }

    /// <summary>Run Assay whenever the package owns a ViewModel or an explicit Assay suite.</summary>
    internal static int RunAssay(string client, string name)
    {
        if (!RequiresAssay(client))
        {
            Console.WriteLine($"af gate — frontend AVP ({name}): not applicable (no ViewModel or Assay suite).");
            return 0;
        }

        Console.WriteLine($"af gate — frontend AVP ({name})...");
        // Invoke Assay directly: a package script is allowed to compose work, but must not be able to replace
        // the acceptance verifier with a placeholder that exits zero.
        return Tooling.Run("npx", ["--no-install", "assay", "verify"], client);
    }

    /// <summary>Decide whether the universal ViewModel-to-Assay obligation applies to this package.</summary>
    internal static bool RequiresAssay(string client)
    {
        var source = Path.Combine(client, "src");
        if (!Directory.Exists(source))
            return false;

        return Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)
            .Select(Path.GetFileName)
            .Any(file => file is not null &&
                (file.EndsWith(".viewModel.ts", StringComparison.OrdinalIgnoreCase)
                 || file.EndsWith(".viewModel.tsx", StringComparison.OrdinalIgnoreCase)
                 || file.Contains(".assay.test.", StringComparison.OrdinalIgnoreCase)));
    }

    private static int FeatureCoverage(string workspace, IReadOnlyList<FrontendPackage> targets)
    {
        if (targets.Count == 0)
            return 0;

        Console.WriteLine("af gate — frontend feature → E2E coverage...");
        var arguments = new List<string> { "--no-install", "affe-feature-e2e", workspace };
        arguments.AddRange(targets.Select(target =>
            $"{(target.Role == FrontendPackageRole.Surface ? "surface" : "core")}={target.Path}"));
        var toolRoot = targets.FirstOrDefault(target => target.Role == FrontendPackageRole.Surface)?.Path
            ?? targets[0].Path;
        return Tooling.Run("npx", [.. arguments], toolRoot);
    }
}
