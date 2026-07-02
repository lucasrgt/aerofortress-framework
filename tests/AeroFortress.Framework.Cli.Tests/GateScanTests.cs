using AeroFortress.Framework.Cli;

namespace AeroFortress.Framework.Cli.Tests;

public class GateScanTests
{
    [Fact]
    public void ScanProofs_finds_the_criterion_its_class_and_its_method()
    {
        var root = NewDir();
        Write(root, "Withdraw.Avp.Tests.cs", """
            namespace Sample.Tests.Modules.Wallets;

            public class WithdrawAvpProof
            {
                [AVP("idempotency-key-honored")]
                [Integration]
                [Fact]
                public async Task Withdraw_honors_the_idempotency_key()
                {
                }
            }
            """);

        var proof = Assert.Single(GateScan.ScanProofs(root));

        Assert.Equal("idempotency-key-honored", proof.CriterionId);
        Assert.Equal("WithdrawAvpProof", proof.ClassName);
        Assert.Equal("Withdraw_honors_the_idempotency_key", proof.Method);
    }

    [Fact]
    public void ScanProofs_never_walks_into_dependency_or_build_dirs()
    {
        var root = NewDir();
        Write(Path.Combine(root, "node_modules", "pkg"), "Dep.cs", "[AVP(\"from-a-dependency\")] class D {}");
        Write(Path.Combine(root, "bin", "Debug"), "Out.cs", "[AVP(\"from-build-output\")] class O {}");

        Assert.Empty(GateScan.ScanProofs(root));
    }

    [Fact]
    public void ScanSlices_reads_the_attribute_block_and_the_namespace_module()
    {
        var root = NewDir();
        Write(root, "Withdraw.cs", """
            namespace Sample.Api.Modules.Wallets;

            /// <summary>Withdraw money.</summary>
            [Slice]
            [Critical]
            public static class Withdraw
            {
            }
            """);
        Write(root, "Plain.cs", """
            namespace Sample.Api.Modules.Wallets;

            public static class NotASlice
            {
            }
            """);

        var slice = Assert.Single(GateScan.ScanSlices(root));

        Assert.Equal(("Wallets", "Withdraw", true), (slice.Module, slice.Name, slice.Critical));
    }

    [Fact]
    public void DiscoverManifests_parses_the_wellformed_and_reports_the_malformed()
    {
        var root = NewDir();
        Write(root, "Wallets.spec.toml", """
            module = "Wallets"
            [slices.Withdraw]
            criteria = ["idempotency-key-honored"]
            """);
        Write(root, "Broken.spec.toml", "just some text with no module key");

        var manifests = GateScan.DiscoverManifests(root);

        var wallets = Assert.Single(manifests, m => m.Manifest is not null);
        Assert.Equal("Wallets", wallets.Manifest!.Module);
        Assert.Equal(new[] { "idempotency-key-honored" }, wallets.Manifest.Slices["Withdraw"]);
        var broken = Assert.Single(manifests, m => m.Manifest is null);
        Assert.NotNull(broken.Error);
    }

    [Fact]
    public void ParseTrxDirectory_joins_definitions_to_results()
    {
        var dir = NewDir();
        Write(dir, "run.trx", """
            <?xml version="1.0" encoding="UTF-8"?>
            <TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
              <Results>
                <UnitTestResult testId="one" outcome="Passed" />
                <UnitTestResult testId="two" outcome="Failed" />
              </Results>
              <TestDefinitions>
                <UnitTest id="one">
                  <TestMethod className="Sample.Tests.WithdrawAvpProof, Sample.Tests" name="Honors_the_key" />
                </UnitTest>
                <UnitTest id="two">
                  <TestMethod className="Sample.Tests.DepositAvpProof" name="Never_overdraws" />
                </UnitTest>
              </TestDefinitions>
            </TestRun>
            """);

        var verdicts = GateScan.ParseTrxDirectory(dir);

        Assert.Equal(2, verdicts.Count);
        Assert.Contains(verdicts, v =>
            v is { ClassName: "Sample.Tests.WithdrawAvpProof", Method: "Honors_the_key", Outcome: "Passed" });
        Assert.Contains(verdicts, v =>
            v is { ClassName: "Sample.Tests.DepositAvpProof", Method: "Never_overdraws", Outcome: "Failed" });
    }

    private static string NewDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "af-gate-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void Write(string dir, string name, string content)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, name), content);
    }
}
