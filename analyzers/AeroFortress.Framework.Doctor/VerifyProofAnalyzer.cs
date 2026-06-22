using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AeroFortress.Framework.Doctor;

/// <summary>
/// AF0030 — a <c>[Verify("id")]</c> obligation must have a matching AVP proof: somewhere in the
/// compilation or its test files there is an <c>[AVP("id")]</c> verification for the same criterion id.
/// This couples the static doctor to the runtime verifier (AVP / Assay.Net): the framework refuses to
/// build a slice that declares it must be proven against a criterion it never proves.
///
/// AVP proofs live in test files excluded from the app's compilation, so the analyzer reads them as
/// <c>AdditionalFiles</c> (and also scans the compilation), matching ids textually — the same trade-off
/// as <c>AF0008</c>. Detection is by attribute name, so the framework takes no dependency on the AVP
/// package and the relation stays one-way (framework knows AVP, never the reverse).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class VerifyProofAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported for a <c>[Verify]</c> obligation with no matching <c>[AVP]</c> proof.</summary>
    public const string DiagnosticId = "AF0030";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Verify obligation must have an AVP proof",
        messageFormat: "Criterion '{0}' is declared with [Verify] but has no [AVP(\"{0}\")] proof; add an AVP verification for it",
        category: "AeroFortress.Framework.Convention",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [Verify(\"id\")] declares that code must be proven against an AVP acceptance criterion; "
                   + "the build fails until an [AVP(\"id\")] verification for the same id exists.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    // Matches [AVP("criterion-id")] / [AVPAttribute("...")] in a test file, optionally namespace-qualified.
    private static readonly Regex AvpProofPattern = new(
        "\\bAVP(?:Attribute)?\\s*\\(\\s*\"(?<id>[^\"]+)\"",
        RegexOptions.Compiled);

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
        var required = new ConcurrentBag<(string Id, Location Location)>();
        var proven = new ConcurrentBag<string>();

        context.RegisterSyntaxNodeAction(syntax =>
        {
            var attribute = (AttributeSyntax)syntax.Node;
            var id = StringArgument(attribute);
            if (id.Length == 0)
                return;

            if (IsNamed(attribute, "Verify"))
                required.Add((id, attribute.GetLocation()));
            else if (IsNamed(attribute, "AVP"))
                proven.Add(id);
        }, SyntaxKind.Attribute);

        context.RegisterCompilationEndAction(end =>
        {
            if (required.IsEmpty)
                return;

            var provenIds = new HashSet<string>(proven);
            foreach (var file in end.Options.AdditionalFiles)
            {
                var text = file.GetText(end.CancellationToken)?.ToString();
                if (text is null)
                    continue;
                foreach (Match match in AvpProofPattern.Matches(text))
                    provenIds.Add(match.Groups["id"].Value);
            }

            foreach (var (id, location) in required)
                if (!provenIds.Contains(id))
                    end.ReportDiagnostic(Diagnostic.Create(Rule, location, id));
        });
    }

    /// <summary>True when the attribute's written name is <paramref name="marker"/> (with or without the
    /// <c>Attribute</c> suffix, possibly namespace-qualified) — a syntactic match needing no symbol reference.</summary>
    private static bool IsNamed(AttributeSyntax attribute, string marker)
    {
        var name = attribute.Name.ToString();
        return name == marker
            || name == marker + "Attribute"
            || name.EndsWith("." + marker)
            || name.EndsWith("." + marker + "Attribute");
    }

    /// <summary>The first constructor argument as a string literal, or empty when there is none.</summary>
    private static string StringArgument(AttributeSyntax attribute)
    {
        var first = attribute.ArgumentList?.Arguments.FirstOrDefault();
        return first?.Expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
                ? literal.Token.ValueText
                : string.Empty;
    }
}
