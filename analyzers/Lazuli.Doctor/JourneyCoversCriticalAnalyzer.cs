using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Lazuli.Doctor;

/// <summary>
/// LZ0010 — a <c>[Journey]</c> proves a <c>[Critical]</c> slice, and nothing else. A
/// <c>[Journey(typeof(X), …)]</c> whose <c>X</c> is not a <c>[Critical]</c> slice is rejected: its only
/// consumer is <c>LZ0008</c>, which tracks critical slices, so a journey on a non-critical slice is inert
/// metadata — it reads as coverage but enforces nothing, and nothing would warn you. Either the slice is
/// genuinely high-stakes — mark it <c>[Critical]</c> and the journey becomes its required proof — or it is
/// not, and a plain <c>[E2E]</c> flow test (no <c>[Journey]</c>) is the right tool.
///
/// Together with <c>LZ0008</c> (every critical slice has its happy + sad journeys) this makes the relation
/// bidirectional: a slice is <c>[Critical]</c> exactly when it carries journeys, and a journey exists
/// exactly for a critical slice. Journeys live in test files excluded from the app build, so — like
/// <c>LZ0008</c> — they are read from <c>AdditionalFiles</c> and matched textually.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JourneyCoversCriticalAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier reported for a journey that covers a non-critical slice.</summary>
    public const string DiagnosticId = "LZ0010";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "A journey must cover a [Critical] slice",
        messageFormat: "[Journey] covers '{0}', which is not a [Critical] slice — mark '{0}' [Critical] (the "
                     + "journey becomes its required proof) or drop [Journey] and keep a plain [E2E] flow test",
        category: "Lazuli.Convention",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A [Journey] is the LZ0008-tracked proof of a [Critical] slice; one pointing at a "
                   + "non-critical slice is inert. Mark the slice [Critical] or use a plain [E2E] test.",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    // Mirrors LZ0008's journey grammar: [Journey(typeof(Slice), JourneyPath.Happy|Sad)] — the covers: prefix
    // and the enum qualifier are optional, and the slice may be written qualified (last segment is the name).
    private static readonly Regex JourneyPattern = new(
        @"Journey\s*\(\s*(?:covers\s*:\s*)?typeof\s*\(\s*(?<slice>[\w.]+)\s*\)\s*,\s*(?:\w+\s*\.\s*)?(?:Happy|Sad)\b",
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
        // What counts as critical follows the active policy — so under strict, a journey on an undecided
        // slice (now treated as critical) is valid, while a journey on a [NonCritical] slice is still inert.
        var level = CriticalityPolicy.Read(context.Options.AnalyzerConfigOptionsProvider);
        var critical = new ConcurrentBag<string>();

        context.RegisterSyntaxNodeAction(syntax =>
        {
            var cls = (ClassDeclarationSyntax)syntax.Node;
            if (CriticalityPolicy.IsCriticalUnderPolicy(cls, level))
                critical.Add(cls.Identifier.Text);
        }, SyntaxKind.ClassDeclaration);

        context.RegisterCompilationEndAction(end =>
        {
            var criticalSet = new HashSet<string>(critical);
            foreach (var file in end.Options.AdditionalFiles)
            {
                var source = file.GetText(end.CancellationToken);
                var text = source?.ToString();
                if (text is null)
                    continue;

                foreach (Match match in JourneyPattern.Matches(text))
                {
                    var group = match.Groups["slice"];
                    var dot = group.Value.LastIndexOf('.');
                    var slice = dot >= 0 ? group.Value.Substring(dot + 1) : group.Value;
                    if (criticalSet.Contains(slice))
                        continue;

                    var span = new TextSpan(group.Index, group.Length);
                    var location = Location.Create(file.Path, span, source!.Lines.GetLinePositionSpan(span));
                    end.ReportDiagnostic(Diagnostic.Create(Rule, location, slice));
                }
            }
        });
    }
}
