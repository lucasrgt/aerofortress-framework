using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AeroFortress.Framework.Doctor;

/// <summary>
/// AF0008 — every write slice must be proven end-to-end on both paths: it needs a happy journey and at
/// least one sad journey covering it. Write depth is derived from ordinary code — persistence calls and
/// endpoint verbs — rather than a self-selected risk marker. An ambiguous slice receives the stronger bar.
///
/// Journeys live in test files excluded from the app's compilation, so the analyzer reads them as
/// <c>AdditionalFiles</c> and matches each write slice against the <c>[Journey(typeof(Slice),
/// JourneyPath.Happy|Sad)]</c> declarations it finds. The match is textual — the same trade-off as
/// <c>AF0003</c> — which is enough to enforce that both journeys exist.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WriteJourneyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported for a write slice missing a journey.</summary>
    public const string DiagnosticId = "AF0008";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Write slice must have happy and sad journeys",
        messageFormat: "Write slice '{0}' is missing a {1} journey; add [Journey(typeof({0}), JourneyPath.{1})] to an [E2E] test",
        category: "AeroFortress.Framework.Convention",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Every write slice must be proven end-to-end on the happy path and at least one sad "
                   + "path; the sad journey asserts the failure status and that no state changed. The operation "
                   + "shape determines the obligation, so application code cannot opt out.",
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
        var writes = new ConcurrentBag<(string Name, Location Location)>();

        context.RegisterSyntaxNodeAction(syntax =>
        {
            var cls = (ClassDeclarationSyntax)syntax.Node;
            if (VerificationDepthPolicy.RequiresJourneys(cls))
                writes.Add((cls.Identifier.Text, cls.Identifier.GetLocation()));
        }, SyntaxKind.ClassDeclaration);

        context.RegisterCompilationEndAction(end =>
        {
            if (writes.IsEmpty)
                return;

            var covered = CoveredJourneys(end.Options.AdditionalFiles, end.CancellationToken);
            foreach (var (name, location) in writes)
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
}
