using AeroFortress.Framework.Cli;
using Assay.Net;

namespace AeroFortress.Framework.Cli.Tests;

public class GateMatrixTests
{
    [Fact]
    public void A_declared_criterion_with_a_passing_proof_is_pass()
    {
        var matrix = GateMatrix.Build(
            [Manifest("Wallets", "[slices.Withdraw]", "criteria = [\"idempotency-key-honored\"]")],
            [new AvpProof("idempotency-key-honored", "W.Avp.Tests.cs", "WithdrawAvpProof", "Honors_the_key")],
            [new SliceSite("Wallets", "Withdraw", Critical: true, "W.cs")],
            [new TestVerdict("Sample.Tests.WithdrawAvpProof", "Honors_the_key", "Passed")]);

        var row = Assert.Single(matrix.Rows);
        Assert.Equal(MatrixVerdict.Pass, row.Verdict);
        Assert.False(matrix.Blocking);
    }

    [Fact]
    public void A_failing_proof_fails_the_criterion_and_blocks_the_gate()
    {
        var matrix = GateMatrix.Build(
            [Manifest("Wallets", "[slices.Withdraw]", "criteria = [\"idempotency-key-honored\"]")],
            [new AvpProof("idempotency-key-honored", "W.Avp.Tests.cs", "WithdrawAvpProof", "Honors_the_key")],
            [new SliceSite("Wallets", "Withdraw", Critical: true, "W.cs")],
            [new TestVerdict("Sample.Tests.WithdrawAvpProof", "Honors_the_key", "Failed")]);

        Assert.Equal(MatrixVerdict.Fail, Assert.Single(matrix.Rows).Verdict);
        Assert.True(matrix.Blocking);
    }

    [Fact]
    public void A_declared_criterion_with_no_proof_is_the_AF0030_gap()
    {
        var matrix = GateMatrix.Build(
            [Manifest("Wallets", "[slices.Withdraw]", "criteria = [\"no-overdraw\"]")],
            proofs: [],
            [new SliceSite("Wallets", "Withdraw", Critical: false, "W.cs")],
            verdicts: []);

        Assert.Equal(MatrixVerdict.NoProof, Assert.Single(matrix.Rows).Verdict);
        Assert.True(matrix.Blocking);
    }

    [Fact]
    public void A_proof_with_no_decided_outcome_is_not_run_never_a_pass()
    {
        // The AVP stance: a skipped/never-executed proof must not read as green.
        var matrix = GateMatrix.Build(
            [Manifest("Wallets", "[slices.Withdraw]", "criteria = [\"idempotency-key-honored\"]")],
            [new AvpProof("idempotency-key-honored", "W.Avp.Tests.cs", "WithdrawAvpProof", "Honors_the_key")],
            [new SliceSite("Wallets", "Withdraw", Critical: false, "W.cs")],
            [new TestVerdict("Sample.Tests.WithdrawAvpProof", "Honors_the_key", "NotExecuted")]);

        Assert.Equal(MatrixVerdict.NotRun, Assert.Single(matrix.Rows).Verdict);
        Assert.True(matrix.Blocking);
    }

    [Fact]
    public void A_proof_no_manifest_declares_is_creep()
    {
        var matrix = GateMatrix.Build(
            [Manifest("Wallets", "[slices.Withdraw]", "criteria = [\"idempotency-key-honored\"]")],
            [
                new AvpProof("idempotency-key-honored", "W.Avp.Tests.cs", "WithdrawAvpProof", "Honors_the_key"),
                new AvpProof("stray-criterion", "S.Avp.Tests.cs", "StrayProof", "Proves_something_undeclared"),
            ],
            [new SliceSite("Wallets", "Withdraw", Critical: false, "W.cs")],
            [new TestVerdict("Sample.Tests.WithdrawAvpProof", "Honors_the_key", "Passed")]);

        var orphan = Assert.Single(matrix.OrphanProofs);
        Assert.Equal("stray-criterion", orphan.CriterionId);
        Assert.True(matrix.Blocking);
    }

    [Fact]
    public void A_critical_slice_with_no_nonempty_declaration_is_reported()
    {
        // Charge is [Critical] but its module manifest gives it an empty criteria array — the same
        // "declares nothing" AF0031 refuses; a table with no criteria is not a declaration.
        var matrix = GateMatrix.Build(
            [Manifest("Payments", "[slices.Charge]", "criteria = []")],
            proofs: [],
            [new SliceSite("Payments", "Charge", Critical: true, "Charge.cs")],
            verdicts: []);

        var undeclared = Assert.Single(matrix.UndeclaredCritical);
        Assert.Equal("Charge", undeclared.Name);
        Assert.True(matrix.Blocking);
    }

    [Fact]
    public void One_passing_proof_covers_every_slice_declaring_the_id()
    {
        // The binding is per-criterion-id, repo-wide (the representative-proof model): two slices in two
        // modules declare the same id; the single passing proof turns both rows green.
        var matrix = GateMatrix.Build(
            [
                Manifest("Wallets", "[slices.Withdraw]", "criteria = [\"idempotency-key-honored\"]"),
                Manifest("Payments", "[slices.Charge]", "criteria = [\"idempotency-key-honored\"]"),
            ],
            [new AvpProof("idempotency-key-honored", "W.Avp.Tests.cs", "WithdrawAvpProof", "Honors_the_key")],
            [
                new SliceSite("Wallets", "Withdraw", Critical: false, "W.cs"),
                new SliceSite("Payments", "Charge", Critical: false, "C.cs"),
            ],
            [new TestVerdict("Sample.Tests.WithdrawAvpProof", "Honors_the_key", "Passed")]);

        Assert.Equal(2, matrix.Rows.Count);
        Assert.All(matrix.Rows, r => Assert.Equal(MatrixVerdict.Pass, r.Verdict));
        Assert.False(matrix.Blocking);
    }

    [Fact]
    public void A_malformed_manifest_blocks_but_a_declared_slice_without_class_is_a_note()
    {
        var broken = new ManifestFile("Broken.spec.toml", null, "spec.toml has no 'module' key.");
        var matrix = GateMatrix.Build(
            [broken, Manifest("Wallets", "[slices.Ghost]", "criteria = [\"idempotency-key-honored\"]")],
            [new AvpProof("idempotency-key-honored", "W.Avp.Tests.cs", "WithdrawAvpProof", "Honors_the_key")],
            slices: [],
            [new TestVerdict("Sample.Tests.WithdrawAvpProof", "Honors_the_key", "Passed")]);

        Assert.Single(matrix.MalformedManifests);
        var note = Assert.Single(matrix.DeclaredWithoutClass);
        Assert.Equal("Ghost", note.Slice);
        Assert.True(matrix.Blocking);

        // Drop the malformed file: only the informational note remains, and the matrix no longer blocks.
        var healthy = GateMatrix.Build(
            [Manifest("Wallets", "[slices.Ghost]", "criteria = [\"idempotency-key-honored\"]")],
            [new AvpProof("idempotency-key-honored", "W.Avp.Tests.cs", "WithdrawAvpProof", "Honors_the_key")],
            slices: [],
            [new TestVerdict("Sample.Tests.WithdrawAvpProof", "Honors_the_key", "Passed")]);
        Assert.Single(healthy.DeclaredWithoutClass);
        Assert.False(healthy.Blocking);
    }

    private static ManifestFile Manifest(string module, params string[] body) =>
        new(module + ".spec.toml",
            SpecManifest.Parse($"module = \"{module}\"\n" + string.Join('\n', body)),
            null);
}
