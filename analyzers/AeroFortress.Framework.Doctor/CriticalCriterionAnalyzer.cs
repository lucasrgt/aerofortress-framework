using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AeroFortress.Framework.Doctor;

/// <summary>
/// AF0031 — a slice that is <c>[Critical]</c> under the active policy must declare at least one acceptance
/// criterion in its module's <c>&lt;Module&gt;.spec.toml</c> (the Clockwork spec manifest): a
/// <c>[slices.&lt;Name&gt;]</c> table with a non-empty <c>criteria</c> array. It is the reverse of
/// <c>AF0030</c> on the other axis — AF0030 forces a declared criterion to have an <c>[AVP]</c> proof
/// (criterion ⟹ proof); this forces a high-stakes behaviour to declare a criterion in the first place
/// (critical behaviour ⟹ criterion). Together they close the spec-driven bridge in both directions, so a
/// <c>[Critical]</c> slice can never ship proving nothing at the AVP layer.
///
/// The obligation moved off the inline <c>[Verify]</c> attribute onto the manifest (read via
/// <see cref="SpecManifest"/>), so a slice's acceptance spec is a reviewable file beside it rather than
/// source noise. "Critical" is read through <see cref="CriticalityPolicy"/>, so AF0008, AF0010, AF0029 and
/// this rule agree on what critical means under opt-in / explicit / strict. It is one rung finer than
/// <c>AF0008</c>'s end-to-end journeys — a journey proves the path runs; the AVP criterion proves a named
/// property holds.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CriticalCriterionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported for a critical slice not declared in its module's spec manifest.</summary>
    public const string DiagnosticId = "AF0031";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Critical slice must declare a criterion in its spec manifest",
        messageFormat: "critical slice '{0}' declares no acceptance criterion in {1}; add a "
                     + "[slices.{0}] table with a non-empty criteria array (proven by a matching [AVP(\"id\")])",
        category: "AeroFortress.Framework.Convention",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [Critical] slice is high-stakes; beyond its end-to-end journeys (AF0008) it must name at "
                   + "least one AVP acceptance criterion in its module's <Module>.spec.toml — the reverse of AF0030 "
                   + "— so a high-stakes slice can never ship proving nothing at the criterion layer.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(OnStart);
    }

    private static void OnStart(CompilationStartAnalysisContext context)
    {
        // Read the dial once: the same policy AF0008/AF0010/AF0029 obey, so all four agree on "critical".
        var level = CriticalityPolicy.Read(context.Options.AnalyzerConfigOptionsProvider);

        // The critical slices collected during syntax analysis, each remembered with its module and a
        // location to report on; the manifest is only available (as AdditionalFiles) at compilation end.
        var criticalSlices = new ConcurrentBag<(string Module, string Slice, Location Location)>();

        context.RegisterSyntaxNodeAction(node =>
        {
            var cls = (ClassDeclarationSyntax)node.Node;
            if (!CriticalityPolicy.IsCriticalUnderPolicy(cls, level))
                return;
            var module = ModuleNaming.ModuleOf(cls);
            if (module is not null)
                criticalSlices.Add((module, cls.Identifier.Text, cls.Identifier.GetLocation()));
        }, SyntaxKind.ClassDeclaration);

        context.RegisterCompilationEndAction(end =>
        {
            if (criticalSlices.IsEmpty)
                return;

            var manifests = SpecManifest.ReadAll(end.Options.AdditionalFiles, end.CancellationToken);

            foreach (var (module, slice, location) in criticalSlices)
            {
                var expected = module + SpecManifest.Suffix;
                var declared = manifests.TryGetValue(module, out var manifest)
                    && !manifest.CriteriaFor(slice).IsEmpty;
                if (!declared)
                    end.ReportDiagnostic(Diagnostic.Create(Rule, location, slice, expected));
            }
        });
    }
}
