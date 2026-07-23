using System.Text.RegularExpressions;

namespace AeroFortress.Framework.Cli;

/// <summary>
/// Rejects repository-local escape hatches that can hide an AeroFortress diagnostic from the release gate.
/// Advisory tools remain useful during development, but a committed suppression would make the mandatory
/// contract depend on author discipline instead of enforcement.
/// </summary>
internal static partial class SuppressionGate
{
    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".aerofortress",
        ".expo",
        ".git",
        ".next",
        ".nuxt",
        "artifacts",
        "bin",
        "client.gen",
        "coverage",
        "dist",
        "local-feed",
        "node_modules",
        "obj",
        "TestResults",
        "tmp",
        "vendor",
    };

    private static readonly HashSet<string> ScannedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs",
        ".csproj",
        ".cjs",
        ".editorconfig",
        ".globalconfig",
        ".js",
        ".json",
        ".mjs",
        ".props",
        ".targets",
        ".ts",
        ".tsx",
        ".yaml",
        ".yml",
    };

    /// <summary>Scan the workspace and print every release-contract suppression as an actionable error.</summary>
    internal static int Run(string workspace)
    {
        var findings = Find(workspace);
        if (findings.Count == 0)
        {
            Console.WriteLine("af gate — suppression policy: no AeroFortress escape hatches.");
            return 0;
        }

        Console.Error.WriteLine(
            $"af gate — suppression policy: {findings.Count} AeroFortress escape hatch(es) found.");
        foreach (var finding in findings)
            Console.Error.WriteLine($"  {finding.Path}:{finding.Line}: {finding.Reason}");
        Console.Error.WriteLine(
            "Remove the suppression and satisfy the rule; release obligations cannot be downgraded locally.");
        return 1;
    }

    /// <summary>Return deterministic findings so the scanner contract can be tested without invoking a gate.</summary>
    internal static IReadOnlyList<SuppressionFinding> Find(string workspace)
    {
        var root = Path.GetFullPath(workspace);
        var findings = new List<SuppressionFinding>();
        foreach (var file in EnumerateFiles(new DirectoryInfo(root)))
        {
            var relative = Path.GetRelativePath(root, file.FullName).Replace('\\', '/');
            var content = File.ReadAllText(file.FullName);
            foreach (var (pattern, reason) in RejectedPatterns())
            {
                foreach (Match match in pattern.Matches(content))
                    findings.Add(new SuppressionFinding(relative, LineNumber(content, match.Index), reason));
            }
            if (file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
                && FrameworkDeclaration().IsMatch(content)
                && (GeneratedCodeMarker().IsMatch(content) || GeneratedFileName().IsMatch(file.Name)))
            {
                findings.Add(new SuppressionFinding(
                    relative,
                    1,
                    "framework declarations cannot impersonate generated code to bypass AF diagnostics."));
            }
        }

        return findings
            .OrderBy(finding => finding.Path, StringComparer.Ordinal)
            .ThenBy(finding => finding.Line)
            .ToArray();
    }

    private static IEnumerable<FileInfo> EnumerateFiles(DirectoryInfo directory)
    {
        foreach (var file in directory.EnumerateFiles())
        {
            if (ScannedExtensions.Contains(file.Extension)
                || file.Name.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase)
                || file.Name.Equals(".globalconfig", StringComparison.OrdinalIgnoreCase))
            {
                yield return file;
            }
        }

        foreach (var child in directory.EnumerateDirectories())
        {
            if (IgnoredDirectories.Contains(child.Name))
                continue;
            foreach (var file in EnumerateFiles(child))
                yield return file;
        }
    }

    private static IReadOnlyList<(Regex Pattern, string Reason)> RejectedPatterns() =>
    [
        (PragmaSuppression(), "AF diagnostics cannot be disabled with #pragma."),
        (AttributeSuppression(), "AF diagnostics cannot be hidden with SuppressMessage."),
        (MsBuildSuppression(), "AF diagnostics cannot be excluded from the compiler warning policy."),
        (DiagnosticSeveritySuppression(), "AF diagnostic severity cannot be lowered below warning."),
        (AnalyzerSeveritySuppression(), "the analyzer severity floor cannot hide AeroFortress diagnostics."),
        (GeneratedCodeConfiguration(), "generated_code configuration cannot disable AF analysis."),
        (EslintInlineSuppression(), "AFFE diagnostics cannot be disabled inline."),
        (EslintConfigSuppression(), "AFFE rules cannot be disabled in ESLint configuration."),
    ];

    private static int LineNumber(string content, int offset)
    {
        var line = 1;
        for (var index = 0; index < offset; index++)
        {
            if (content[index] == '\n')
                line++;
        }

        return line;
    }

    [GeneratedRegex(
        @"^\s*#pragma\s+warning\s+disable(?:\s*(?://.*)?$|[^\r\n]*\bAF\d{4}\b)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex PragmaSuppression();

    [GeneratedRegex(
        @"^\s*\[\s*(?:System\.Diagnostics\.CodeAnalysis\.)?SuppressMessage\b[^\]]*?\bAF\d{4}\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex AttributeSuppression();

    [GeneratedRegex(
        @"^\s*<(?:NoWarn|WarningsNotAsErrors)>[\s\S]*?\bAF(?:\d{4}|\*)\b[\s\S]*?</(?:NoWarn|WarningsNotAsErrors)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex MsBuildSuppression();

    [GeneratedRegex(
        @"^\s*dotnet_diagnostic\.AF\d{4}\.severity\s*=\s*(?:none|silent|suggestion)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex DiagnosticSeveritySuppression();

    [GeneratedRegex(
        @"^\s*dotnet_analyzer_diagnostic(?:\.category-[^=\s]+)?\.severity\s*=\s*(?:none|silent|suggestion)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex AnalyzerSeveritySuppression();

    [GeneratedRegex(
        @"^\s*generated_code\s*=\s*true\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex GeneratedCodeConfiguration();

    [GeneratedRegex(
        @"^\s*(?://|/\*|\*)\s*eslint-disable(?:-line|-next-line)?[^\r\n]*\baerofortress/",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex EslintInlineSuppression();

    [GeneratedRegex(
        @"(?:['""]aerofortress/[^'""]+['""]|aerofortress/[a-z0-9_-]+)\s*:\s*(?:['""]off['""]|off|0)(?=\s*(?:[,;}\]]|$))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EslintConfigSuppression();

    [GeneratedRegex(
        @"\[\s*(?:[\w.]+\.)?(?:Slice|Entity|ValueObject|AVP|Journey)(?:Attribute)?(?:\s|\(|\])",
        RegexOptions.CultureInvariant)]
    private static partial Regex FrameworkDeclaration();

    [GeneratedRegex(
        @"(?:^\s*//\s*<auto-generated\b|\[\s*(?:[\w.]+\.)?GeneratedCode(?:Attribute)?\s*\()",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex GeneratedCodeMarker();

    [GeneratedRegex(
        @"(?:\.g|\.generated|\.designer)\.cs$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GeneratedFileName();
}

/// <summary>A source location whose local configuration would weaken the release contract.</summary>
/// <param name="Path">Workspace-relative path.</param>
/// <param name="Line">One-based line number.</param>
/// <param name="Reason">Actionable rejection reason.</param>
internal sealed record SuppressionFinding(string Path, int Line, string Reason);
