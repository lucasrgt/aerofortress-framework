namespace AeroFortress.Framework.Cli;

/// <summary>The four executable verification legs for one frontend client.</summary>
/// <param name="Client">The client directory name.</param>
/// <param name="Tests">The unit/integration test script exit code.</param>
/// <param name="Avp">The Assay/AVP verification script exit code.</param>
/// <param name="E2eShape">The E2E manifest/spec/runner doctor exit code.</param>
/// <param name="E2e">The real E2E runner script exit code.</param>
internal sealed record FrontendGateLeg(string Client, int Tests, int Avp, int E2eShape, int E2e)
{
    /// <summary>Whether every frontend verification leg ran successfully.</summary>
    public bool Green => Tests == 0 && Avp == 0 && E2eShape == 0 && E2e == 0;
}

/// <summary>Runs every frontend proof suite. Missing scripts/tools fail naturally; nothing is optional.</summary>
internal static class FrontendGate
{
    /// <summary>Run unit/integration tests, Assay, the E2E doctor, and the real E2E suite for every client.</summary>
    public static IReadOnlyList<FrontendGateLeg> Run(IEnumerable<string> clients)
    {
        var legs = new List<FrontendGateLeg>();
        foreach (var client in clients)
        {
            var name = Path.GetFileName(client);
            Console.WriteLine($"af gate — frontend tests ({name})...");
            var tests = FrontendScriptContract.Run(client, "test");

            Console.WriteLine($"af gate — frontend AVP ({name})...");
            // Invoke Assay directly: a package script is allowed to compose work, but must not be able to replace
            // the acceptance verifier with a placeholder that exits zero.
            var avp = Tooling.Run("npx", ["--no-install", "assay", "verify"], client);

            Console.WriteLine($"af gate — frontend E2E contract ({name})...");
            var e2eShape = Tooling.Run("npx", ["--no-install", "affe-e2e-doctor", "."], client);

            Console.WriteLine($"af gate — frontend E2E execution ({name})...");
            var e2e = FrontendScriptContract.Run(client, "test:e2e");
            legs.Add(new FrontendGateLeg(name, tests, avp, e2eShape, e2e));
        }

        return legs;
    }
}
