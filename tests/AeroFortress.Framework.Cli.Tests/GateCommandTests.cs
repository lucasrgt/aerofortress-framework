using AeroFortress.Framework.Cli;

namespace AeroFortress.Framework.Cli.Tests;

public class GateCommandTests
{
    [Fact]
    public void A_green_doctor_reuses_its_successful_build_for_the_proof_run()
    {
        var arguments = GateCommand.ProofArguments(["App.slnx", "-c", "Release"], 0, "evidence");

        Assert.Contains("--no-build", arguments);
        Assert.Equal("evidence", arguments[^1]);
    }

    [Fact]
    public void A_failed_doctor_never_reuses_potentially_stale_binaries()
    {
        var arguments = GateCommand.ProofArguments(["App.slnx"], 1, "evidence");

        Assert.DoesNotContain("--no-build", arguments);
    }
}
