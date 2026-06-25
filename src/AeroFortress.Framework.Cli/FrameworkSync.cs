using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AeroFortress.Framework.Cli;

/// <summary>
/// The anti-desync leg of <c>af doctor</c> — the enforcement half of the package-first law. A pilot consumes
/// framework behavior through released packages; framework-shaped source copies and stale package declarations
/// are failures, not alternate distribution mechanisms.
/// </summary>
internal static class FrameworkSync
{
    private static readonly string[] DependencySections =
        ["dependencies", "devDependencies", "peerDependencies", "optionalDependencies"];

    /// <summary>The outcome of the sync check.</summary>
    /// <param name="Gating">True when the framework checkout was reachable and the comparisons actually ran.</param>
    /// <param name="InSync">True when nothing gating drifted (vacuously true when not <paramref name="Gating"/>).</param>
    /// <param name="Messages">Human-readable notices/failures.</param>
    public record Outcome(bool Gating, bool InSync, IReadOnlyList<string> Messages);

    /// <summary>Run the sync check for the app at <paramref name="root"/>.</summary>
    public static Outcome Check(string root)
    {
        var manifest = Path.Combine(root, AeroFortressManifest.FileName);
        if (!File.Exists(manifest))
            return new Outcome(false, true, []);

        var repoRel = Regex.Match(File.ReadAllText(manifest), @"^\s*repo\s*=\s*""([^""]+)""", RegexOptions.Multiline);
        if (!repoRel.Success)
            return new Outcome(false, true,
                ["framework-sync: no [framework] repo in AeroFortress.toml — drift against the framework checkout is not checked (declare it on dev machines)"]);

        var repo = Path.GetFullPath(Path.Combine(root, repoRel.Groups[1].Value));
        if (!Directory.Exists(repo))
            return new Outcome(false, true,
                [$"framework-sync: framework repo '{repoRel.Groups[1].Value}' not found — skipping (normal on CI; on a dev machine, fix the path)"]);

        var messages = new List<string>();
        CheckPackageVersions(root, repo, messages);
        CheckFrontendPackages(root, repo, messages);
        CheckLegacyFrontendCopies(root, messages);
        return new Outcome(Gating: true, InSync: messages.Count == 0, Messages: messages);
    }

    private static void CheckPackageVersions(string root, string repo, List<string> messages)
    {
        var props = Path.Combine(repo, "build", "AeroFortress.Framework.Library.props");
        if (!File.Exists(props))
            return;
        var current = Regex.Match(File.ReadAllText(props), @"<Version>([^<]+)</Version>");
        if (!current.Success)
            return;

        var stale = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(IsConsumerFile)
            .SelectMany(p => Regex.Matches(File.ReadAllText(p), @"Include=""(AeroFortress[^""]*)""\s+Version=""([^""]+)""")
                .Where(m => m.Groups[2].Value != current.Groups[1].Value)
                .Select(m => $"{m.Groups[1].Value} {m.Groups[2].Value} in {Path.GetFileName(p)}"))
            .Distinct()
            .ToList();

        if (stale.Count > 0)
            messages.Add(
                $"framework-sync: the framework is at {current.Groups[1].Value} but this app references "
                + string.Join(", ", stale)
                + " — bump the package(s), rebuild, and fix what the doctor reveals (the package-first law)");
    }

    private static void CheckFrontendPackages(string root, string repo, List<string> messages)
    {
        if (!HasFrontend(root))
            return;

        foreach (var canonicalPath in new[]
                 {
                     Path.Combine(repo, "frontend-sdk", "package.json"),
                     Path.Combine(repo, "frontend-sdk", "packages", "eslint-plugin", "package.json"),
                     Path.Combine(repo, "frontend-sdk", "packages", "aerofortress-react", "package.json"),
                 })
        {
            if (!TryReadPackage(canonicalPath, out var packageName, out var currentVersion))
                continue;

            var declarations = FindDeclarations(root, packageName).ToList();
            if (declarations.Count == 0)
            {
                messages.Add(
                    $"framework-sync: frontend exists but no package.json declares {packageName} {currentVersion} — "
                    + "consume the published framework package instead of carrying its source locally");
                continue;
            }

            var stale = declarations
                .Where(d => DeclaredVersion(d.Spec) != currentVersion)
                .Select(d => $"{d.Spec} in {Path.GetRelativePath(root, d.Path)}")
                .Distinct()
                .ToList();
            if (stale.Count > 0)
                messages.Add(
                    $"framework-sync: {packageName} canonical is {currentVersion} but this app declares "
                    + string.Join(", ", stale)
                    + " — install the canonical package version and refresh the lockfile");
        }
    }

    private static void CheckLegacyFrontendCopies(string root, List<string> messages)
    {
        var legacyPlugin = Path.Combine(root, "clients", "eslint-plugin-aerofortress");
        if (Directory.Exists(legacyPlugin))
            messages.Add(
                "framework-sync: clients/eslint-plugin-aerofortress is a legacy vendored plugin copy — delete it "
                + "and consume eslint-plugin-aerofortress from npm");
    }

    private static bool HasFrontend(string root)
    {
        var clients = Path.Combine(root, "clients");
        return Directory.Exists(clients)
               && Directory.EnumerateDirectories(clients).Any(dir =>
                   File.Exists(Path.Combine(dir, "package.json"))
                   && new[] { "eslint.config.js", "eslint.config.mjs", "eslint.config.cjs" }
                       .Any(config => File.Exists(Path.Combine(dir, config))));
    }

    private static IEnumerable<(string Path, string Spec)> FindDeclarations(string root, string packageName)
    {
        foreach (var path in Directory.EnumerateFiles(root, "package.json", SearchOption.AllDirectories)
                     .Where(IsConsumerFile))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var section in DependencySections)
            {
                if (!document.RootElement.TryGetProperty(section, out var dependencies)
                    || dependencies.ValueKind != JsonValueKind.Object
                    || !dependencies.TryGetProperty(packageName, out var version)
                    || version.ValueKind != JsonValueKind.String)
                    continue;

                yield return (path, version.GetString()!);
            }
        }
    }

    private static bool TryReadPackage(string path, out string name, out string version)
    {
        name = "";
        version = "";
        if (!File.Exists(path))
            return false;

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("name", out var nameValue)
            || !document.RootElement.TryGetProperty("version", out var versionValue))
            return false;

        name = nameValue.GetString() ?? "";
        version = versionValue.GetString() ?? "";
        return name.Length > 0 && version.Length > 0;
    }

    private static string DeclaredVersion(string spec)
    {
        var match = Regex.Match(spec, @"\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?");
        return match.Success ? match.Value : spec;
    }

    private static bool IsConsumerFile(string path) =>
        !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Any(segment => segment is "node_modules" or ".git" or "bin" or "obj" or "dist");
}
