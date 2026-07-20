namespace AeroFortress.Framework.Cli;

/// <summary>
/// The doctor — one verdict over both halves of the stack, extracted from <c>Program</c> so composite
/// commands (<c>af gate</c>) can run the same legs without duplicating them. The backend leg is the
/// build-time Roslyn analyzers (a clean build = a clean bill of health; an AF#### error fails it). The
/// frontend leg, for each manifest-selected AeroFortress harness package, is
/// the TS-world harness: eslint (eslint-plugin-aerofortress, the AFFE* rules) + tsc (the "wired" gate against
/// the generated client). One command, both sides — so nothing is left loose in either direction.
/// </summary>
internal static class DoctorCommand
{
    /// <summary>Run every doctor leg from the current directory; the exit code is the worst leg's.</summary>
    /// <param name="rest">Extra arguments forwarded to the backend <c>dotnet build</c> (e.g. a solution path).</param>
    public static int Run(string[] rest)
    {
        // The manifest is the topology's source of truth — confirm the project has one and that the paths it
        // declares exist before trusting the rest. A missing manifest is a notice; a broken one fails the doctor.
        var manifest = AeroFortressManifest.Validate(Directory.GetCurrentDirectory());
        Console.WriteLine("af doctor — manifest (AeroFortress.toml)...");
        foreach (var message in manifest.Messages)
            Console.Error.WriteLine($"  {message}");

        // The anti-desync leg (package-first law): a stale AeroFortress.Framework.* package version or a revived
        // vendored frontend copy fails the doctor. The expected version is baked into this CLI, so the gate fires on
        // CI and on any machine — no sibling framework checkout required.
        var sync = FrameworkSync.Check(Directory.GetCurrentDirectory());
        Console.WriteLine("af doctor — framework sync...");
        foreach (var message in sync.Messages)
            Console.Error.WriteLine($"  {message}");

        Console.WriteLine("af doctor — backend conventions (build)...");
        var code = Tooling.Dotnet("build", rest);
        if (manifest.Present && !manifest.Valid)
            code = Math.Max(code, 1);
        if (sync.Gating && !sync.InSync)
            code = Math.Max(code, 1);

        foreach (var client in FrontendClients(Directory.GetCurrentDirectory()))
        {
            Console.WriteLine($"af doctor — frontend conventions ({Path.GetFileName(client)}: eslint + tsc)...");
            code = Math.Max(code, FrontendScriptContract.Run(client, "lint"));
            code = Math.Max(code, FrontendScriptContract.Run(client, "typecheck"));
        }

        Console.WriteLine(code == 0 ? "doctor: conventions pass." : "doctor: violations reported above.");
        return code;
    }

    // The manifest names the real harness package; AeroFortressManifest retains a clients/* compatibility fallback.
    // Requiring an eslint config for discovery made a missing harness file erase the client precisely when the
    // doctor must fail, so package.json — not a healthy config — remains the executable boundary.
    internal static IReadOnlyList<string> FrontendClients(string root)
        => AeroFortressManifest.FrontendPackages(root);
}
