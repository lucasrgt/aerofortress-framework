using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Lazuli.Doctor;

/// <summary>
/// LZ0008 — a slice marked <c>[Critical]</c> must be proven end-to-end on both paths: it needs a
/// happy journey and at least one sad journey covering it. High-stakes operations (money, trust) are
/// where a mishandled failure does the real damage — a partial commit, a wrong status — so the
/// framework demands the failure path be exercised through the booted app, not just the success path.
///
/// Journeys live in test files excluded from the app's compilation, so the analyzer reads them as
/// <c>AdditionalFiles</c> and matches each critical slice against the <c>[Journey(typeof(Slice),
/// JourneyPath.Happy|Sad)]</c> declarations it finds. The match is textual — the same trade-off as
/// <c>LZ0003</c> — which is enough to enforce that both journeys exist.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CriticalJourneyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported for a critical slice missing a journey.</summary>
    public const string DiagnosticId = "LZ0008";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Critical slice must have happy and sad journeys",
        messageFormat: "Critical slice '{0}' is missing a {1} journey; add [Journey(typeof({0}), JourneyPath.{1})] to an [E2E] test",
        category: "Lazuli.Convention",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [Critical] slice must be proven end-to-end on the happy path and at least one sad "
                   + "path; the sad journey asserts the failure status and that no state changed.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    // Matches [Journey(typeof(Slice), JourneyPath.Happy)] — covers: prefix and the enum qualifier are
    // both optional, and the slice may be written qualified (the last segment is the type name).
    private static readonly Regex JourneyPattern = new(
        @"Journey\s*\(\s*(?:covers\s*:\s*)?typeof\s*\(\s*(?<slice>[\w.]+)\s*\)\s*,\s*(?:\w+\s*\.\s*)?(?<path>Happy|Sad)\b",
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
        var critical = new ConcurrentBag<(string Name, Location Location)>();

        context.RegisterSyntaxNodeAction(syntax =>
        {
            var cls = (ClassDeclarationSyntax)syntax.Node;
            if (HasAttribute(cls, "Slice") && HasAttribute(cls, "Critical"))
                critical.Add((cls.Identifier.Text, cls.Identifier.GetLocation()));
        }, SyntaxKind.ClassDeclaration);

        context.RegisterCompilationEndAction(end =>
        {
            if (critical.IsEmpty)
                return;

            var covered = CoveredJourneys(end.Options.AdditionalFiles, end.CancellationToken);
            foreach (var (name, location) in critical)
            {
                if (!covered.Contains((name, "Happy")))
                    end.ReportDiagnostic(Diagnostic.Create(Rule, location, name, "Happy"));
                if (!covered.Contains((name, "Sad")))
                    end.ReportDiagnostic(Diagnostic.Create(Rule, location, name, "Sad"));
            }
        });
    }

    private static HashSet<(string, string)> CoveredJourneys(ImmutableArray<AdditionalText> files, CancellationToken ct)
    {
        var covered = new HashSet<(string, string)>();
        foreach (var file in files)
        {
            var text = file.GetText(ct)?.ToString();
            if (text is null)
                continue;

            foreach (Match match in JourneyPattern.Matches(text))
            {
                var slice = match.Groups["slice"].Value;
                var dot = slice.LastIndexOf('.');
                if (dot >= 0)
                    slice = slice.Substring(dot + 1);
                covered.Add((slice, match.Groups["path"].Value));
            }
        }

        return covered;
    }

    private static bool HasAttribute(ClassDeclarationSyntax cls, string name) =>
        cls.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attr => attr.Name.ToString())
            .Any(n => n == name || n == name + "Attribute"
                   || n.EndsWith("." + name) || n.EndsWith("." + name + "Attribute"));
}
