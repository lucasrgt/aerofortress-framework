namespace AeroFortress.Framework.Cli;

/// <summary>
/// <c>af gate</c> — the Clockwork done-gate as one deterministic command. It composes the existing legs
/// instead of duplicating them: the doctor (manifest + framework sync + build with the AF* analyzers,
/// including the AF0030/AF0031 bridge + the frontend AFFE* legs), then the proof run (<c>dotnet test</c>,
/// which executes every <c>[AVP]</c> verification), and finally joins declarations × proofs × verdicts into
/// the traceability matrix. The matrix lands as <c>VERIFICATION.md</c> (for humans, committed with the repo)
/// and <c>VERIFICATION.json</c> (for machines); the exit code IS the verdict, so CI and the harness read it
/// without parsing anything.
/// </summary>
internal static class GateCommand
{
    /// <summary>The human-facing matrix artifact, written at the workspace root.</summary>
    public const string MarkdownArtifact = "VERIFICATION.md";

    /// <summary>The machine-facing matrix artifact, written at the workspace root.</summary>
    public const string JsonArtifact = "VERIFICATION.json";

    /// <summary>Run the full gate from the current directory; 0 only when every leg and the matrix hold.</summary>
    /// <param name="rest">Extra arguments forwarded to the build and test legs (e.g. a solution path).</param>
    public static int Run(string[] rest)
    {
        var root = Directory.GetCurrentDirectory();
        Console.WriteLine("af gate — form (doctor) ∧ proof (tests) ∧ the traceability matrix.");
        var doctor = DoctorCommand.Run(rest);

        Console.WriteLine("af gate — proofs (dotnet test)...");
        var results = Path.Combine(Path.GetTempPath(), "af-gate-" + Guid.NewGuid().ToString("N"));
        var tests = Tooling.Dotnet("test", [.. rest, "--logger", "trx", "--results-directory", results]);
        var verdicts = GateScan.ParseTrxDirectory(results);
        TryDelete(results);

        var matrix = GateMatrix.Build(
            GateScan.DiscoverManifests(root),
            GateScan.ScanProofs(root),
            GateScan.ScanSlices(root),
            verdicts);
        var legs = new GateLegs(doctor, tests);

        GateReport.WriteConsole(matrix, legs, Console.Out);
        File.WriteAllText(Path.Combine(root, MarkdownArtifact), GateReport.Markdown(matrix, legs, DateTimeOffset.Now));
        File.WriteAllText(Path.Combine(root, JsonArtifact), GateReport.Json(matrix, legs, DateTimeOffset.Now));
        Console.WriteLine($"gate: wrote {MarkdownArtifact} + {JsonArtifact}.");

        var code = Math.Max(Math.Max(doctor, tests), matrix.Blocking ? 1 : 0);
        Console.WriteLine(code == 0
            ? "gate: GREEN — form, proofs and the matrix all hold."
            : "gate: RED — a leg failed or the matrix has findings (see above).");
        return code;
    }

    // The TRX scratch dir is disposable; a locked file on Windows must never fail the gate itself.
    private static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
