using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace AeroFortress.Framework.Cli;

/// <summary>
/// The anti-desync leg of <c>af doctor</c> — the enforcement half of the <b>package-first law</b>
/// (framework-shaped code lands in lazuli-net first; a pilot consumes it as a versioned package and a
/// rebased plugin mirror, never as code written in the pilot and "ported back someday").
///
/// A pilot opts in by declaring where the framework checkout lives in <c>AeroFortress.toml</c>:
/// <code>
/// [framework]
/// repo = "../../lazuli-net"
/// </code>
/// When that path resolves (a dev machine with the sibling checkout), the doctor compares two drift-prone
/// surfaces and fails on a mismatch:
/// <list type="bullet">
/// <item><description><b>Package version</b> — the app's referenced <c>AeroFortress*</c> package versions vs the
/// framework's current <c>Version</c> (<c>build/AeroFortress.Framework.Library.props</c>). Behind = the pilot is running an
/// old doctor and silently missing rules.</description></item>
/// <item><description><b>The eslint-plugin mirror</b> — <c>clients/eslint-plugin-aerofortress/index.cjs</c> hashed
/// against the canonical <c>frontend-sdk/packages/eslint-plugin/index.cjs</c>. Different = the mirror missed
/// a rule wave (or worse, grew a local rule that never went upstream).</description></item>
/// </list>
/// When the section is absent, or the repo path doesn't resolve (CI, another machine), the check degrades to
/// a notice and gates nothing — the same posture as the manifest itself.
/// </summary>
internal static class FrameworkSync
{
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
        CheckPluginMirror(root, repo, messages);
        return new Outcome(Gating: true, InSync: messages.Count == 0, Messages: messages);
    }

    // The app's AeroFortress* package references must match the framework's current version — behind means the
    // pilot runs an old doctor and silently misses rules; ahead means a phantom version.
    private static void CheckPackageVersions(string root, string repo, List<string> messages)
    {
        var props = Path.Combine(repo, "build", "AeroFortress.Framework.Library.props");
        if (!File.Exists(props))
            return;
        var current = Regex.Match(File.ReadAllText(props), @"<Version>([^<]+)</Version>");
        if (!current.Success)
            return;

        var stale = Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Where(p => !p.Contains("node_modules"))
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

    // The plugin mirror must be byte-identical to the canonical — a drifted mirror either missed a rule wave
    // or grew a local rule that never went upstream; both are the desync this leg exists to catch.
    private static void CheckPluginMirror(string root, string repo, List<string> messages)
    {
        var mirror = Path.Combine(root, "clients", "eslint-plugin-aerofortress", "index.cjs");
        var canonical = Path.Combine(repo, "frontend-sdk", "packages", "eslint-plugin", "index.cjs");
        if (!File.Exists(mirror) || !File.Exists(canonical))
            return;

        if (!HashOf(mirror).SequenceEqual(HashOf(canonical)))
            messages.Add(
                "framework-sync: clients/eslint-plugin-aerofortress/index.cjs differs from the framework canonical — "
                + "rebase the mirror (copy index.cjs + index.test.cjs from frontend-sdk/packages/eslint-plugin, "
                + "bump its package.json version) and adopt any new rules in the app's eslint config");
    }

    // Line-ending-insensitive: the mirror is copied across repos whose git eol normalization may differ,
    // and a CRLF/LF-only difference is not drift.
    private static byte[] HashOf(string path) =>
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(path).Replace("\r\n", "\n")));
}
