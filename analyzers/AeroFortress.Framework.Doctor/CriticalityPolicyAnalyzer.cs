using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AeroFortress.Framework.Doctor;

/// <summary>
/// LZ0029 — under the <c>"explicit"</c> criticality policy, a <c>[Slice]</c> must resolve its criticality:
/// it carries <c>[Critical]</c> (high-stakes, journeys required) or <c>[NonCritical]</c> (a reviewed
/// downgrade), never neither. The mirror of <c>LZ0022</c>'s posture on authorization — criticality becomes
/// a <i>decision</i> on every slice, made visible and reviewable, instead of an absence that reads the same
/// whether it was considered or forgotten.
///
/// The rule is policy-gated and inert outside <c>"explicit"</c>: under the default <c>"opt-in"</c> there is
/// nothing to force (only <c>[Critical]</c> matters), and under <c>"strict"</c> the "undecided ⇒ critical"
/// effect lives in <c>LZ0008</c>/<c>LZ0010</c> (which then demand the journeys), not here. So an unmarked
/// slice is an error only where the workspace asked for an explicit decision on each one.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CriticalityPolicyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported for an undecided slice under the explicit policy.</summary>
    public const string DiagnosticId = "LZ0029";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Slice must decide its criticality under the explicit policy",
        messageFormat: "Slice '{0}' declares neither [Critical] nor [NonCritical]; the 'explicit' criticality "
                     + "policy requires every slice to decide — mark it [Critical] (journeys required) or [NonCritical]",
        category: "AeroFortress.Framework.Convention",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Under the 'explicit' criticality policy ([testing] criticality in AeroFortress.toml) every "
                   + "[Slice] must carry [Critical] or [NonCritical] — criticality is a decision on each slice, "
                   + "like authorization (LZ0022), not an omission that could be a choice or an oversight.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            // The dial is compilation-wide; read it once, and only arm the per-class check under "explicit".
            if (CriticalityPolicy.Read(start.Options.AnalyzerConfigOptionsProvider) != CriticalityPolicy.Level.Explicit)
                return;

            start.RegisterSyntaxNodeAction(ctx =>
            {
                var cls = (ClassDeclarationSyntax)ctx.Node;
                if (!CriticalityPolicy.IsSlice(cls))
                    return;
                if (CriticalityPolicy.HasAttribute(cls, "Critical") || CriticalityPolicy.HasAttribute(cls, "NonCritical"))
                    return;

                ctx.ReportDiagnostic(Diagnostic.Create(Rule, cls.Identifier.GetLocation(), cls.Identifier.Text));
            }, SyntaxKind.ClassDeclaration);
        });
    }
}
