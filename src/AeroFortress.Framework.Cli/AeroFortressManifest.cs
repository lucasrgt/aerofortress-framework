using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AeroFortress.Framework.Cli;

/// <summary>
/// Reads + validates the workspace manifest, <c>AeroFortress.toml</c> — the single source of truth for an app's
/// topology (which dirs are the backend, the frontend core, the platform apps; which harness gates the
/// build). The manifest was the spine the monorepo design promised but the CLI never read; this is the
/// first reader so <c>af doctor</c> can confirm a project HAS one and that the paths it declares exist.
///
/// The check is deliberately light (the keys it gates, not a full TOML parse): a missing manifest is a
/// notice (older projects predate it), but a present-yet-broken one — no <c>[workspace]</c>, no name, a
/// declared backend/core path that isn't on disk, or a backend that doesn't pin its dev environment
/// (<see cref="CheckBackendDevEnv"/>) — is a reported failure.
/// </summary>
internal static class AeroFortressManifest
{
    /// <summary>The manifest filename, at the repo root.</summary>
    public const string FileName = "AeroFortress.toml";

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
                Messages: [$"no {FileName} — the workspace manifest is the single source of truth; scaffold one with `af new`"]);

        var text = File.ReadAllText(path);
        var messages = new List<string>();

        if (!Regex.IsMatch(text, @"^\s*\[workspace\]", RegexOptions.Multiline))
            messages.Add($"{FileName}: missing a [workspace] section");
        else if (!Regex.IsMatch(text, @"^\s*name\s*=", RegexOptions.Multiline))
            messages.Add($"{FileName}: [workspace] declares no name");

        // Every backend/core path a product declares must resolve on disk, so the manifest can't drift from
        // the real tree (the topology it claims is the topology that exists). A declared backend (the .NET API)
        // must also pin its dev environment (see CheckBackendDevEnv).
        foreach (Match m in Regex.Matches(text, @"^\s*(backend|core)\s*=\s*""([^""]+)""", RegexOptions.Multiline))
        {
            var kind = m.Groups[1].Value;
            var rel = m.Groups[2].Value;
            var full = Path.Combine(root, rel);
            if (!Directory.Exists(full) && !File.Exists(full))
            {
                messages.Add($"{FileName}: {kind} path '{rel}' does not exist");
                continue;
            }

            if (kind == "backend" && Directory.Exists(full))
                CheckBackendDevEnv(full, rel, messages);
        }

        CheckCriticalityPolicy(text, messages);

        return new Outcome(Present: true, Valid: messages.Count == 0, Messages: messages);
    }

    /// <summary>
    /// The <c>[testing] criticality</c> dial (the doctor's criticality policy) must, when present, name one of
    /// the three known levels — <c>"opt-in"</c>, <c>"explicit"</c>, <c>"strict"</c>. A typo would otherwise
    /// fall back silently to <c>opt-in</c> at build time (the projection only matches a known keyword), so a
    /// misspelled <c>"strickt"</c> reads as "no policy" with no warning. The check is textual, like the rest of
    /// this validator; an absent key is fine (absence means <c>opt-in</c>).
    /// </summary>
    private static void CheckCriticalityPolicy(string text, List<string> messages)
    {
        var match = Regex.Match(text, @"^\s*criticality\s*=\s*""([^""]*)""", RegexOptions.Multiline);
        if (match.Success && match.Groups[1].Value is not ("opt-in" or "explicit" or "strict"))
            messages.Add(
                $"{FileName}: [testing] criticality must be \"opt-in\", \"explicit\", or \"strict\" (got " +
                $"\"{match.Groups[1].Value}\") — an unknown value silently falls back to opt-in at build time.");
    }

    /// <summary>
    /// A declared <c>backend</c> (the .NET API) must pin its dev environment in
    /// <c>Properties/launchSettings.json</c>: <c>ASPNETCORE_ENVIRONMENT=Development</c> and an
    /// <c>applicationUrl</c>. Without the file a `dotnet run` silently defaults to <b>Production</b> on the .NET
    /// default port — which both drifts the port the frontend points at and flips on Production-gated behavior
    /// (rate limiting → 429 on register/login). One missing file caused both in a pilot; this catches it at the
    /// doctor instead of at runtime. The check is textual (the keys it gates, not a full JSON parse), matching
    /// the rest of this validator.
    /// </summary>
    private static void CheckBackendDevEnv(string backendDir, string rel, List<string> messages)
    {
        var launchSettings = Path.Combine(backendDir, "Properties", "launchSettings.json");
        if (!File.Exists(launchSettings))
        {
            messages.Add(
                $"{FileName}: backend '{rel}' has no Properties/launchSettings.json — a dev run then defaults to " +
                "Production on the .NET default port (port drift vs the frontend + Production-gated behavior like " +
                "rate limiting → 429s). Pin ASPNETCORE_ENVIRONMENT=Development and an applicationUrl.");
            return;
        }

        var json = File.ReadAllText(launchSettings);
        if (!Regex.IsMatch(json, @"""ASPNETCORE_ENVIRONMENT""\s*:\s*""Development"""))
            messages.Add(
                $"{FileName}: backend '{rel}' launchSettings.json sets no ASPNETCORE_ENVIRONMENT=Development — a dev " +
                "run defaults to Production (rate limiting on → 429 on register/login while iterating locally).");
        if (!Regex.IsMatch(json, @"""applicationUrl""\s*:\s*""[^""]+"""))
            messages.Add(
                $"{FileName}: backend '{rel}' launchSettings.json pins no applicationUrl — the dev port isn't " +
                "deterministic, so the frontend's base URL can't agree with it by construction.");
    }
}
