using System.Text.Json;
using System.Text.RegularExpressions;

namespace AeroFortress.Framework.Cli;

/// <summary>
/// Refuses missing and placeholder npm scripts before executing a frontend gate leg. The runner remains a swappable
/// package concern, but <c>echo ok</c>, <c>true</c>, and <c>exit 0</c> are never executable evidence.
/// </summary>
internal static class FrontendScriptContract
{
    private static readonly Regex Placeholder = new(
        @"^\s*(?:echo\b[^;&|]*|printf\b[^;&|]*|true|exit\s+0)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Validate and run one required package script, returning a nonzero gate result on contract drift.</summary>
    public static int Run(string packageRoot, string script, params string[] arguments)
    {
        var error = Validate(packageRoot, script);
        if (error is not null)
        {
            Console.Error.WriteLine($"  frontend script contract: {error}");
            return 1;
        }

        return Tooling.Run("npm", ["run", script, .. arguments], packageRoot);
    }

    /// <summary>Return a precise contract error, or <see langword="null"/> when the script is executable.</summary>
    public static string? Validate(string packageRoot, string script)
    {
        var path = Path.Combine(packageRoot, "package.json");
        if (!File.Exists(path))
            return $"{packageRoot} has no package.json";

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("scripts", out var scripts)
                || scripts.ValueKind != JsonValueKind.Object
                || !scripts.TryGetProperty(script, out var value)
                || value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(value.GetString()))
                return $"{Path.GetFileName(packageRoot)} must define an executable `{script}` package script";

            var command = value.GetString()!;
            return Placeholder.IsMatch(command)
                ? $"{Path.GetFileName(packageRoot)} `{script}` is a placeholder (`{command}`), not executable evidence"
                : null;
        }
        catch (JsonException exception)
        {
            return $"{Path.GetFileName(packageRoot)} package.json is invalid: {exception.Message}";
        }
    }
}
