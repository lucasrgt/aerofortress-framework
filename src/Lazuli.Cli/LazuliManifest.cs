using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Lazuli.Cli;

/// <summary>
/// Reads + validates the workspace manifest, <c>Lazuli.toml</c> — the single source of truth for an app's
/// topology (which dirs are the backend, the frontend core, the platform apps; which harness gates the
/// build). The manifest was the spine the monorepo design promised but the CLI never read; this is the
/// first reader so <c>lazuli doctor</c> can confirm a project HAS one and that the paths it declares exist.
///
/// The check is deliberately light (the keys it gates, not a full TOML parse): a missing manifest is a
/// notice (older projects predate it), but a present-yet-broken one — no <c>[workspace]</c>, no name, or a
/// declared backend/core path that isn't on disk — is a reported failure.
/// </summary>
internal static class LazuliManifest
{
    /// <summary>The manifest filename, at the repo root.</summary>
    public const string FileName = "Lazuli.toml";

    /// <summary>The outcome of validating a project's manifest.</summary>
    /// <param name="Present">Whether a <see cref="FileName"/> exists at the root.</param>
    /// <param name="Valid">True when present AND every gated check passed.</param>
    /// <param name="Messages">Human-readable notices/failures (one per problem; one notice when absent).</param>
    public record Outcome(bool Present, bool Valid, IReadOnlyList<string> Messages);

    /// <summary>Validate the manifest at <paramref name="root"/>.</summary>
    public static Outcome Validate(string root)
    {
        var path = Path.Combine(root, FileName);
        if (!File.Exists(path))
            return new Outcome(
                Present: false,
                Valid: false,
                Messages: [$"no {FileName} — the workspace manifest is the single source of truth; scaffold one with `lazuli new`"]);

        var text = File.ReadAllText(path);
        var messages = new List<string>();

        if (!Regex.IsMatch(text, @"^\s*\[workspace\]", RegexOptions.Multiline))
            messages.Add($"{FileName}: missing a [workspace] section");
        else if (!Regex.IsMatch(text, @"^\s*name\s*=", RegexOptions.Multiline))
            messages.Add($"{FileName}: [workspace] declares no name");

        // Every backend/core path a product declares must resolve on disk, so the manifest can't drift from
        // the real tree (the topology it claims is the topology that exists).
        foreach (Match m in Regex.Matches(text, @"^\s*(backend|core)\s*=\s*""([^""]+)""", RegexOptions.Multiline))
        {
            var rel = m.Groups[2].Value;
            var full = Path.Combine(root, rel);
            if (!Directory.Exists(full) && !File.Exists(full))
                messages.Add($"{FileName}: {m.Groups[1].Value} path '{rel}' does not exist");
        }

        return new Outcome(Present: true, Valid: messages.Count == 0, Messages: messages);
    }
}
