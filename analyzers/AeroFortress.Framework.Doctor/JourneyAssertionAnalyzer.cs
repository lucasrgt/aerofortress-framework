using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace AeroFortress.Framework.Doctor;

/// <summary>
/// AF0020 — a <c>[Journey]</c> must assert its post-condition, not merely run. <c>AF0008</c> proves a
/// critical slice HAS its happy + sad journeys; this proves each journey actually checks something — the
/// depth rung above existence. A happy journey asserts the observable effect; a sad journey asserts the
/// failure (and, ideally, that no state changed). An assertion-free journey is theater: it exercises the
/// path and proves nothing.
///
/// Like <c>AF0008</c>/<c>AF0010</c>, journeys live in test files excluded from the app compilation, so this
/// reads them as <c>AdditionalFiles</c> and works textually: for each <c>[Journey(...)]</c> it brace-matches
/// the test method body and looks for an assertion token (<c>Assert</c>, FluentAssertions' <c>Should</c>, or
/// a <c>Verify</c>/<c>Expect</c>/<c>Ensure</c> helper — <c>EnsureSuccessStatusCode()</c> is a real assertion: it throws on failure). Warning-tier — the heuristic is lenient by design (a body that
/// delegates to a custom-named asserter is not flagged), so it catches the empty journey, not the clever one.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JourneyAssertionAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported for a journey whose body asserts nothing.</summary>
    public const string DiagnosticId = "AF0020";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "A journey must assert its post-condition",
        messageFormat: "journey for '{0}' asserts nothing — a [Journey] must check its outcome (a happy journey "
                     + "the observable effect; a sad journey the failure and that no state changed)",
        category: "AeroFortress.Framework.Convention",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [Journey] that runs the path but asserts nothing is theater. Assert the post-condition, or "
                   + "drop [Journey] for a plain [E2E] flow test.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    // [Journey(typeof(Slice), JourneyPath.Happy|Sad)] — mirrors AF0008's grammar; captures the slice for the message.
    private static readonly Regex JourneyPattern = new(
        @"\[\s*Journey\s*\(\s*(?:covers\s*:\s*)?typeof\s*\(\s*(?<slice>[\w.]+)\s*\)\s*,\s*(?:\w+\s*\.\s*)?(?:Happy|Sad)\b[^\]]*\]",
        RegexOptions.Compiled);

    // An assertion token: xUnit Assert.*, FluentAssertions .Should(), a Verify/Expect helper, or an Ensure*
    // call (EnsureSuccessStatusCode throws on failure — a real assertion). Boundary on the left only, so helper
    // names that extend the token (VerifyRejected, AssertBalance) still count — lenient by design: the rule
    // flags the empty journey, not the one that asserts through a custom-named helper.
    private static readonly Regex AssertionPattern = new(@"\b(Assert|Should|Verify|Expect|Ensure)", RegexOptions.Compiled);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(Analyze);
    }

    private static void Analyze(CompilationAnalysisContext context)
    {
        foreach (var file in context.Options.AdditionalFiles)
        {
            var source = file.GetText(context.CancellationToken);
            var text = source?.ToString();
            if (text is null)
                continue;

            // A method may stack happy + sad journeys; report its empty body once (keyed by the body's start).
            var reported = new HashSet<int>();
            foreach (Match match in JourneyPattern.Matches(text))
            {
                var bodyOpen = text.IndexOf('{', match.Index + match.Length);
                if (bodyOpen < 0)
                    continue;
                var bodyClose = MatchBrace(text, bodyOpen);
                if (bodyClose < 0 || !reported.Add(bodyOpen))
                    continue;

                var body = text.Substring(bodyOpen, bodyClose - bodyOpen + 1);
                if (AssertionPattern.IsMatch(body))
                    continue;

                var slice = LastSegment(match.Groups["slice"].Value);
                var span = new TextSpan(match.Index, match.Length);
                var location = Location.Create(file.Path, span, source!.Lines.GetLinePositionSpan(span));
                context.ReportDiagnostic(Diagnostic.Create(Rule, location, slice));
            }
        }
    }

    // Index of the '}' that closes the '{' at <open>, or -1. Brace-counted; good enough for a test method body
    // (a stray brace inside a string literal is the known, accepted limit of the textual approach).
    private static int MatchBrace(string s, int open)
    {
        var depth = 0;
        for (var i = open; i < s.Length; i++)
        {
            if (s[i] == '{')
                depth++;
            else if (s[i] == '}' && --depth == 0)
                return i;
        }
        return -1;
    }

    private static string LastSegment(string qualified)
    {
        var dot = qualified.LastIndexOf('.');
        return dot >= 0 ? qualified.Substring(dot + 1) : qualified;
    }
}
