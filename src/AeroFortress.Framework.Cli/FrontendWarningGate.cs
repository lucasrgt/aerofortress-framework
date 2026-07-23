namespace AeroFortress.Framework.Cli;

/// <summary>
/// Promotes the frontend's revealed warning backlog to a release-gate failure. The package-owned lint script still
/// runs because it may compose contract freshness and framework sync; this leg independently invokes the canonical
/// engines so a script cannot omit <c>--max-warnings=0</c> or run endpoint coverage in advisory mode.
/// </summary>
internal static class FrontendWarningGate
{
    /// <summary>Run strict ESLint for one manifest-selected package.</summary>
    internal static int RunLint(string packageRoot)
    {
        var name = Path.GetFileName(packageRoot);
        var source = Path.Combine(packageRoot, "src");
        if (!Directory.Exists(source))
        {
            Console.Error.WriteLine($"af gate — frontend warnings ({name}): package has no src directory.");
            return 1;
        }

        Console.WriteLine(
            $"af gate — frontend warnings ({name}): every shipped AFFE rule is forced; warning budget is zero...");
        return Tooling.Run(
            "npx",
            ["--no-install", "affe-eslint-gate", "src"],
            packageRoot);
    }

    /// <summary>Run strict app-facing endpoint coverage over every manifest-derived product source union.</summary>
    internal static int RunEndpointCoverage(string workspace)
    {
        var code = 0;
        var environment = new Dictionary<string, string?> { ["AF_GATE"] = "1" };
        foreach (var target in AeroFortressManifest.EndpointCoverageTargets(workspace))
        {
            var sourceDirectory = Directory.GetParent(target.ClientPath)!;
            var owner = Directory.GetParent(sourceDirectory.FullName)!.FullName;
            Console.WriteLine(
                $"af gate — frontend warnings ({Path.GetFileName(owner)}): loose app endpoint budget is zero "
                + $"across {target.SourcePaths.Count} product source root(s)...");
            var arguments = new List<string>
            {
                "--no-install",
                "affe-endpoint-coverage",
                "--strict",
                target.ClientPath,
            };
            arguments.AddRange(target.SourcePaths);
            code = Math.Max(code, Tooling.Run("npx", [.. arguments], owner, environment));
        }

        return code;
    }
}
