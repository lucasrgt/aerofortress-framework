namespace AeroFortress.Framework.Cli;

/// <summary>A matrix criterion's verdict — the traceability outcome, not the raw test outcome.</summary>
internal enum MatrixVerdict
{
    /// <summary>At least one matching proof ran and passed, and none failed.</summary>
    Pass,

    /// <summary>A matching proof failed (or ended in a non-pass, non-skip outcome).</summary>
    Fail,

    /// <summary>A proof site exists but no decided test outcome was found — a skip is never a pass.</summary>
    NotRun,

    /// <summary>The criterion is declared but no <c>[AVP]</c> site proves it (the AF0030 gap).</summary>
    NoProof,
}

/// <summary>One traceability row: module × slice × criterion → its proof sites → the verdict.</summary>
/// <param name="Module">The declaring module (from its <c>&lt;Module&gt;.spec.toml</c>).</param>
/// <param name="Slice">The declared slice name.</param>
/// <param name="CriterionId">The catalog criterion id the slice owes.</param>
/// <param name="Proofs">Every <c>[AVP]</c> site carrying the id — proofs bind repo-wide, one proof covers all declarers.</param>
/// <param name="Verdict">The criterion's outcome for this run.</param>
internal sealed record MatrixRow(
    string Module, string Slice, string CriterionId, IReadOnlyList<AvpProof> Proofs, MatrixVerdict Verdict);

/// <summary>A slice a manifest declares that has no <c>[Slice]</c> class in source — a notice, not a failure.</summary>
/// <param name="Module">The manifest's module.</param>
/// <param name="Slice">The declared slice name with no class found.</param>
/// <param name="ManifestPath">The manifest that declares it.</param>
internal sealed record DeclaredWithoutClass(string Module, string Slice, string ManifestPath);

/// <summary>
/// The verification matrix — the gate's artifact. It joins the four evidence sets the scans collect
/// (manifest declarations, <c>[AVP]</c> proof sites, <c>[Slice]</c> inventory, test verdicts) into the
/// traceability the Clockwork gate demands: every declared criterion → a proof → a decided PASS; no orphan
/// proof (creep), no critical slice declaring nothing, no malformed manifest. Pure — all IO stays in
/// <see cref="GateScan"/> — so every axis is unit-testable.
/// </summary>
internal sealed class GateMatrix
{
    private GateMatrix(
        IReadOnlyList<MatrixRow> rows,
        IReadOnlyList<AvpProof> orphanProofs,
        IReadOnlyList<SliceSite> undeclaredCritical,
        IReadOnlyList<DeclaredWithoutClass> declaredWithoutClass,
        IReadOnlyList<ManifestFile> malformedManifests)
    {
        Rows = rows;
        OrphanProofs = orphanProofs;
        UndeclaredCritical = undeclaredCritical;
        DeclaredWithoutClass = declaredWithoutClass;
        MalformedManifests = malformedManifests;
    }

    /// <summary>The traceability rows, in manifest order (module → slice → criterion).</summary>
    public IReadOnlyList<MatrixRow> Rows { get; }

    /// <summary>Proof sites whose criterion id no manifest declares — scope creep, per the gate's total-matrix rule.</summary>
    public IReadOnlyList<AvpProof> OrphanProofs { get; }

    /// <summary>Explicitly <c>[Critical]</c> slices with no non-empty declaration — the AF0031 axis, mirrored.</summary>
    public IReadOnlyList<SliceSite> UndeclaredCritical { get; }

    /// <summary>Declared slices with no <c>[Slice]</c> class in source (a manifest ahead of its code) — informational.</summary>
    public IReadOnlyList<DeclaredWithoutClass> DeclaredWithoutClass { get; }

    /// <summary>Manifests that exist but could not be parsed — always blocking (a broken contract is no contract).</summary>
    public IReadOnlyList<ManifestFile> MalformedManifests { get; }

    /// <summary>
    /// Whether the matrix alone fails the gate: any non-pass verdict, orphan proof, undeclared critical
    /// slice or malformed manifest. (A declared slice with no class stays informational.)
    /// </summary>
    public bool Blocking =>
        Rows.Any(r => r.Verdict != MatrixVerdict.Pass)
        || OrphanProofs.Count > 0
        || UndeclaredCritical.Count > 0
        || MalformedManifests.Count > 0;

    /// <summary>Join the scanned evidence into the matrix. Pure; order-stable for deterministic reports.</summary>
    /// <param name="manifests">Every discovered <c>*.spec.toml</c> (parsed or malformed).</param>
    /// <param name="proofs">Every <c>[AVP("id")]</c> site in source.</param>
    /// <param name="slices">Every <c>[Slice]</c> class in source.</param>
    /// <param name="verdicts">Every test outcome from the run's TRX files.</param>
    public static GateMatrix Build(
        IReadOnlyList<ManifestFile> manifests,
        IReadOnlyList<AvpProof> proofs,
        IReadOnlyList<SliceSite> slices,
        IReadOnlyList<TestVerdict> verdicts)
    {
        var parsed = manifests.Where(m => m.Manifest is not null).ToList();
        var proofsById = proofs
            .GroupBy(p => p.CriterionId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AvpProof>)g.ToList(), StringComparer.Ordinal);

        var verdictById = proofsById.ToDictionary(
            kv => kv.Key, kv => DecideVerdict(kv.Value, verdicts), StringComparer.Ordinal);

        var rows = new List<MatrixRow>();
        var declaredIds = new HashSet<string>(StringComparer.Ordinal);
        var declaredWithoutClass = new List<DeclaredWithoutClass>();
        foreach (var file in parsed)
        {
            var manifest = file.Manifest!;
            foreach (var (slice, criteria) in manifest.Slices)
            {
                if (!slices.Any(s => s.Module == manifest.Module && s.Name == slice))
                    declaredWithoutClass.Add(new DeclaredWithoutClass(manifest.Module, slice, file.Path));
                foreach (var criterion in criteria)
                {
                    declaredIds.Add(criterion);
                    var sites = proofsById.TryGetValue(criterion, out var found) ? found : [];
                    var verdict = sites.Count == 0 ? MatrixVerdict.NoProof : verdictById[criterion];
                    rows.Add(new MatrixRow(manifest.Module, slice, criterion, sites, verdict));
                }
            }
        }

        var orphans = proofs.Where(p => !declaredIds.Contains(p.CriterionId)).ToList();
        var undeclaredCritical = slices
            .Where(s => s.Critical)
            .Where(s => !parsed.Any(m =>
                m.Manifest!.Module == s.Module
                && m.Manifest.Slices.TryGetValue(s.Name, out var criteria)
                && criteria.Count > 0))
            .ToList();

        return new GateMatrix(
            rows, orphans, undeclaredCritical, declaredWithoutClass,
            manifests.Where(m => m.Manifest is null).ToList());
    }

    // A criterion's verdict from its proof sites and the run's outcomes: any decided failure loses, any
    // decided pass (with no failure) wins, and a proof that never reached a decision is NOT-RUN — the AVP
    // stance that a skip never counts as green.
    private static MatrixVerdict DecideVerdict(IReadOnlyList<AvpProof> sites, IReadOnlyList<TestVerdict> verdicts)
    {
        var outcomes = sites
            .SelectMany(site => verdicts.Where(v => Matches(v, site)))
            .Select(v => v.Outcome)
            .ToList();

        if (outcomes.Any(o => o is not ("Passed" or "NotExecuted")))
            return MatrixVerdict.Fail;
        if (outcomes.Contains("Passed"))
            return MatrixVerdict.Pass;
        return MatrixVerdict.NotRun;
    }

    // TRX class names are namespace-qualified; proof classes are bare type names from the textual scan.
    private static bool Matches(TestVerdict verdict, AvpProof proof) =>
        verdict.Method == proof.Method
        && (verdict.ClassName == proof.ClassName
            || verdict.ClassName.EndsWith("." + proof.ClassName, StringComparison.Ordinal));
}
