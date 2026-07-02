using System.Text.RegularExpressions;
using System.Xml.Linq;
using Assay.Net;

namespace AeroFortress.Framework.Cli;

/// <summary>An <c>[AVP("id")]</c> proof site: which test method, in which class and file, proves a criterion.</summary>
/// <param name="CriterionId">The catalog criterion id the proof carries.</param>
/// <param name="File">The proof file, relative to the workspace root.</param>
/// <param name="ClassName">The declaring class (unqualified), used to match the test-run verdict.</param>
/// <param name="Method">The proof method name, used to match the test-run verdict.</param>
internal sealed record AvpProof(string CriterionId, string File, string ClassName, string Method);

/// <summary>A <c>[Slice]</c> class found in source: the code-side inventory the manifest is checked against.</summary>
/// <param name="Module">The module the slice belongs to (namespace-derived, path fallback).</param>
/// <param name="Name">The slice class name.</param>
/// <param name="Critical">Whether the class carries an explicit <c>[Critical]</c> attribute.</param>
/// <param name="File">The slice file, relative to the workspace root.</param>
internal sealed record SliceSite(string Module, string Name, bool Critical, string File);

/// <summary>One test method's outcome from a TRX result file, keyed the way proofs are matched.</summary>
/// <param name="ClassName">The fully-qualified test class from the TRX definition.</param>
/// <param name="Method">The test method name.</param>
/// <param name="Outcome">The raw TRX outcome (<c>Passed</c>, <c>Failed</c>, <c>NotExecuted</c>, …).</param>
internal sealed record TestVerdict(string ClassName, string Method, string Outcome);

/// <summary>A discovered <c>&lt;Module&gt;.spec.toml</c>: parsed when well-formed, an error otherwise.</summary>
/// <param name="Path">The manifest path, relative to the workspace root.</param>
/// <param name="Manifest">The parsed manifest, or <c>null</c> when it could not be read.</param>
/// <param name="Error">The reason the manifest could not be read, when <paramref name="Manifest"/> is null.</param>
internal sealed record ManifestFile(string Path, SpecManifest? Manifest, string? Error);

/// <summary>
/// The gate's evidence collectors: the <c>*.spec.toml</c> declarations, the <c>[AVP]</c> proof sites, the
/// <c>[Slice]</c> inventory and the test-run verdicts. Everything is gathered textually — the same trade-off
/// the doctor makes (AF0030 scans AdditionalFiles by regex) — so the matrix and the analyzers agree on what
/// counts as a proof. The scans only REPORT; enforcement stays with the doctor's symbol-aware rules.
/// </summary>
internal static class GateScan
{
    private static readonly string[] SkippedDirs =
        ["bin", "obj", "node_modules", ".git", "dist", "coverage", "vendor", "local-feed"];

    // Mirrors the doctor's AvpProofPattern (VerifyProofAnalyzer) so both ends agree on what a proof is.
    private static readonly Regex AvpPattern =
        new("\\bAVP(?:Attribute)?\\s*\\(\\s*\"(?<id>[^\"]+)\"", RegexOptions.Compiled);

    private static readonly Regex MethodPattern =
        new(@"\b(?:async\s+)?(?:Task|ValueTask|void)\s*(?:<[^>\n]+>)?\s+(?<name>\w+)\s*\(", RegexOptions.Compiled);

    private static readonly Regex ClassPattern = new(@"\bclass\s+(?<name>\w+)", RegexOptions.Compiled);

    private static readonly Regex ModuleNamespacePattern =
        new(@"namespace\s+[\w.]+\.Modules\.(?<module>\w+)", RegexOptions.Compiled);

    /// <summary>Find and parse every <c>*.spec.toml</c> under <paramref name="root"/>; a malformed one is reported, never thrown.</summary>
    public static IReadOnlyList<ManifestFile> DiscoverManifests(string root)
    {
        var manifests = new List<ManifestFile>();
        foreach (var file in Walk(root, "*.spec.toml"))
        {
            try
            {
                manifests.Add(new ManifestFile(Relative(root, file), SpecManifest.Load(file), null));
            }
            catch (Exception e) when (e is FormatException or IOException)
            {
                manifests.Add(new ManifestFile(Relative(root, file), null, e.Message));
            }
        }

        return manifests;
    }

    /// <summary>Collect every <c>[AVP("id")]</c> site in the workspace's C# sources, with its class and method.</summary>
    public static IReadOnlyList<AvpProof> ScanProofs(string root)
    {
        var proofs = new List<AvpProof>();
        foreach (var file in Walk(root, "*.cs"))
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("AVP", StringComparison.Ordinal))
                continue;

            var lines = text.Split('\n');
            var currentClass = "";
            for (var i = 0; i < lines.Length; i++)
            {
                var cls = ClassPattern.Match(lines[i]);
                if (cls.Success)
                    currentClass = cls.Groups["name"].Value;

                foreach (Match m in AvpPattern.Matches(lines[i]))
                    proofs.Add(new AvpProof(
                        m.Groups["id"].Value, Relative(root, file), currentClass, MethodBelow(lines, i + 1)));
            }
        }

        return proofs;
    }

    /// <summary>Collect every class carrying <c>[Slice]</c> (and whether it also carries <c>[Critical]</c>).</summary>
    /// <remarks>
    /// The scan reads the attribute lines immediately above (or on) the class line, so it mirrors the shapes the
    /// scaffolder emits. Criticality derived from a <em>policy</em> (not the attribute) is the doctor's domain
    /// (AF0031 reads <c>CriticalityPolicy</c>); the matrix reports the explicit marks.
    /// </remarks>
    public static IReadOnlyList<SliceSite> ScanSlices(string root)
    {
        var slices = new List<SliceSite>();
        foreach (var file in Walk(root, "*.cs"))
        {
            var text = File.ReadAllText(file);
            if (!text.Contains("[Slice]", StringComparison.Ordinal))
                continue;

            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var cls = ClassPattern.Match(lines[i]);
                if (!cls.Success)
                    continue;
                var block = AttributeBlockAbove(lines, i);
                if (!block.Contains("[Slice]", StringComparison.Ordinal))
                    continue;
                slices.Add(new SliceSite(
                    ModuleOf(text, file),
                    cls.Groups["name"].Value,
                    block.Contains("[Critical]", StringComparison.Ordinal),
                    Relative(root, file)));
            }
        }

        return slices;
    }

    /// <summary>Parse every <c>*.trx</c> in <paramref name="resultsDirectory"/> into per-method outcomes.</summary>
    public static IReadOnlyList<TestVerdict> ParseTrxDirectory(string resultsDirectory)
    {
        var verdicts = new List<TestVerdict>();
        if (!Directory.Exists(resultsDirectory))
            return verdicts;

        foreach (var trx in Directory.EnumerateFiles(resultsDirectory, "*.trx", SearchOption.AllDirectories))
        {
            XDocument doc;
            try
            {
                doc = XDocument.Load(trx);
            }
            catch (System.Xml.XmlException)
            {
                continue;   // a truncated result file cannot contribute verdicts; the test exit code still gates
            }

            XNamespace ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";
            var definitions = doc.Descendants(ns + "UnitTest")
                .Select(t => (Id: (string?)t.Attribute("id"), Method: t.Element(ns + "TestMethod")))
                .Where(t => t.Id is not null && t.Method is not null)
                .ToDictionary(
                    t => t.Id!,
                    t => (Class: ClassOnly((string?)t.Method!.Attribute("className") ?? ""),
                          Name: (string?)t.Method!.Attribute("name") ?? ""));

            foreach (var result in doc.Descendants(ns + "UnitTestResult"))
            {
                var id = (string?)result.Attribute("testId");
                if (id is null || !definitions.TryGetValue(id, out var test))
                    continue;
                verdicts.Add(new TestVerdict(test.Class, test.Name, (string?)result.Attribute("outcome") ?? "NotExecuted"));
            }
        }

        return verdicts;
    }

    // The TRX className may carry an ", Assembly" suffix; the type name is what proofs are matched on.
    private static string ClassOnly(string className)
    {
        var comma = className.IndexOf(',');
        return comma < 0 ? className : className[..comma];
    }

    // The first method declaration under an [AVP] attribute — walked a bounded number of lines so an attribute
    // stack ([AVP] + [Integration] + [Fact]) still lands on its method without ever crossing into the next member.
    private static string MethodBelow(string[] lines, int from)
    {
        for (var i = from; i < lines.Length && i < from + 12; i++)
        {
            var m = MethodPattern.Match(lines[i]);
            if (m.Success)
                return m.Groups["name"].Value;
        }

        return "";
    }

    // The attribute lines directly above (and on) a class line — the region [Slice]/[Critical] live in.
    private static string AttributeBlockAbove(string[] lines, int classLine)
    {
        var block = lines[classLine];
        for (var j = classLine - 1; j >= 0; j--)
        {
            var t = lines[j].Trim();
            if (t.Length == 0 || t.StartsWith('[') || t.StartsWith("//", StringComparison.Ordinal))
                block = lines[j] + "\n" + block;
            else
                break;
        }

        return block;
    }

    // A slice's module: the `namespace <App>.Modules.<M>` line when present, else the Modules/<M>/ path segment.
    private static string ModuleOf(string text, string file)
    {
        var ns = ModuleNamespacePattern.Match(text);
        if (ns.Success)
            return ns.Groups["module"].Value;

        var segments = file.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var modules = Array.IndexOf(segments, "Modules");
        return modules >= 0 && modules + 1 < segments.Length ? segments[modules + 1] : "";
    }

    // Depth-first file walk that skips build/dependency dirs — enumerating into node_modules would dominate the scan.
    private static IEnumerable<string> Walk(string root, string pattern)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            foreach (var sub in Directory.EnumerateDirectories(dir))
                if (!SkippedDirs.Contains(Path.GetFileName(sub), StringComparer.OrdinalIgnoreCase))
                    stack.Push(sub);
            foreach (var file in Directory.EnumerateFiles(dir, pattern))
                yield return file;
        }
    }

    // Forward slashes keep the committed artifacts identical across OSes (the matrix travels with the repo).
    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace('\\', '/');
}
