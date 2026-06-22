using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AeroFortress.Framework.Doctor;

/// <summary>
/// LZ0031 — a slice that is <c>[Critical]</c> under the active policy must declare at least one proven
/// acceptance criterion: a <c>[Verify("id")]</c> on the slice or a method within it. It is the reverse of
/// <c>LZ0030</c> on the other axis — LZ0030 forces a declared criterion to have an <c>[AVP]</c> proof
/// (criterion ⟹ proof); this forces a high-stakes behaviour to declare a criterion in the first place
/// (critical behaviour ⟹ criterion). Together they close the spec-driven bridge in both directions, so a
/// <c>[Critical]</c> slice can never ship proving nothing at the AVP layer.
///
/// "Critical" is read through <see cref="CriticalityPolicy"/>, so LZ0008, LZ0010, LZ0029 and this rule agree
/// on what critical means under opt-in / explicit / strict. It is one rung finer than <c>LZ0008</c>'s
/// end-to-end journeys — a journey proves the path runs; the AVP criterion proves a named property holds.
/// Detection is syntactic by attribute name, so the framework takes no dependency on the AVP package (the
/// relation stays one-way: framework knows AVP, never the reverse).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CriticalCriterionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported for a critical slice that declares no <c>[Verify]</c> criterion.</summary>
    public const string DiagnosticId = "LZ0031";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Critical slice must declare a proven criterion",
        messageFormat: "Critical slice '{0}' declares no acceptance criterion; add a [Verify(\"id\")] "
                     + "(with a matching [AVP(\"id\")] proof) to the slice or a method within it",
        category: "AeroFortress.Framework.Convention",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [Critical] slice is high-stakes; beyond its end-to-end journeys (LZ0008) it must name at "
                   + "least one AVP acceptance criterion via [Verify(\"id\")] — the reverse of LZ0030 — so a "
                   + "high-stakes slice can never ship proving nothing at the criterion layer.");

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
        // Read the dial once: the same policy LZ0008/LZ0010/LZ0029 obey, so all four agree on "critical".
        var level = CriticalityPolicy.Read(context.Options.AnalyzerConfigOptionsProvider);

        context.RegisterSyntaxNodeAction(node =>
        {
            var cls = (ClassDeclarationSyntax)node.Node;
            if (!CriticalityPolicy.IsCriticalUnderPolicy(cls, level))
                return;

            if (!DeclaresCriterion(cls))
                node.ReportDiagnostic(Diagnostic.Create(Rule, cls.Identifier.GetLocation(), cls.Identifier.Text));
        }, SyntaxKind.ClassDeclaration);
    }

    /// <summary>True when the slice or a member within it carries a <c>[Verify]</c> criterion — the criterion
    /// may sit on the slice class or on its <c>Handle</c> method, so the whole class subtree is searched.</summary>
    private static bool DeclaresCriterion(ClassDeclarationSyntax cls) =>
        cls.DescendantNodes().OfType<AttributeSyntax>().Any(IsVerify);

    /// <summary>Whether the attribute's written name is <c>Verify</c> (with or without the <c>Attribute</c>
    /// suffix, possibly namespace-qualified) — a syntactic match needing no symbol reference.</summary>
    private static bool IsVerify(AttributeSyntax attribute)
    {
        var name = attribute.Name.ToString();
        return name == "Verify" || name == "VerifyAttribute"
            || name.EndsWith(".Verify") || name.EndsWith(".VerifyAttribute");
    }
}
