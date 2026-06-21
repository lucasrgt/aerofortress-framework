using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AeroFortress.Framework.Doctor;

/// <summary>
/// The shared reading of the workspace's <b>criticality policy</b> — the one place that answers "is this
/// slice critical right now?" so <c>LZ0008</c>, <c>LZ0010</c>, and <c>LZ0029</c> agree by construction.
///
/// The policy is a single dial (<c>[testing] criticality</c> in <c>AeroFortress.toml</c>) projected to the
/// analyzers as the MSBuild property <c>AeroFortressCriticality</c> — the doctor's <c>buildTransitive</c> targets
/// read the TOML and surface it through <c>CompilerVisibleProperty</c>, so the analyzer never parses TOML and
/// stays removable with the rest of the harness. Three levels:
/// <list type="bullet">
/// <item><description><see cref="Level.OptIn"/> (default, and the meaning of an absent dial): only a
/// <c>[Critical]</c> slice is critical — today's behavior, unchanged.</description></item>
/// <item><description><see cref="Level.Explicit"/>: criticality is still opt-in for the test bar, but every
/// slice must <i>decide</i> — carry <c>[Critical]</c> or <c>[NonCritical]</c> — or <c>LZ0029</c> errors.</description></item>
/// <item><description><see cref="Level.Strict"/>: an undecided slice is <i>treated as</i> <c>[Critical]</c>;
/// the only opt-out is the explicit <c>[NonCritical]</c> marker.</description></item>
/// </list>
/// </summary>
internal static class CriticalityPolicy
{
    /// <summary>The <c>GlobalOptions</c> key the doctor's targets project the dial onto (a
    /// <c>CompilerVisibleProperty</c> surfaces an MSBuild property as <c>build_property.&lt;name&gt;</c>).</summary>
    public const string OptionKey = "build_property.AeroFortressCriticality";

    /// <summary>The three policy levels a workspace can choose.</summary>
    public enum Level
    {
        /// <summary>Default: only a <c>[Critical]</c> slice is critical (today's behavior).</summary>
        OptIn,

        /// <summary>Every slice must carry <c>[Critical]</c> or <c>[NonCritical]</c> (else <c>LZ0029</c>).</summary>
        Explicit,

        /// <summary>An undecided slice is treated as <c>[Critical]</c>; <c>[NonCritical]</c> is the opt-out.</summary>
        Strict,
    }

    /// <summary>Read the active policy from the projected MSBuild property; an absent or unrecognized value
    /// is <see cref="Level.OptIn"/>, so a workspace that never set the dial keeps today's behavior.</summary>
    public static Level Read(AnalyzerConfigOptionsProvider provider)
    {
        if (provider.GlobalOptions.TryGetValue(OptionKey, out var raw) && raw is not null)
        {
            switch (raw.Trim().ToLowerInvariant())
            {
                case "explicit": return Level.Explicit;
                case "strict": return Level.Strict;
                case "opt-in": return Level.OptIn;
            }
        }

        return Level.OptIn;
    }

    /// <summary>Whether a class is a slice (carries <c>[Slice]</c>).</summary>
    public static bool IsSlice(ClassDeclarationSyntax cls) => HasAttribute(cls, "Slice");

    /// <summary>
    /// Whether a slice counts as critical under <paramref name="level"/> — the single definition the three
    /// rules share. A non-slice is never critical. A <c>[Critical]</c> slice always is. Under
    /// <see cref="Level.Strict"/> an undecided slice (no <c>[NonCritical]</c>) is critical too. Under
    /// <see cref="Level.OptIn"/> / <see cref="Level.Explicit"/> this reduces to "<c>[Slice]</c> and
    /// <c>[Critical]</c>" — identical to the pre-policy check.
    /// </summary>
    public static bool IsCriticalUnderPolicy(ClassDeclarationSyntax cls, Level level)
    {
        if (!HasAttribute(cls, "Slice"))
            return false;

        return HasAttribute(cls, "Critical")
            || (level == Level.Strict && !HasAttribute(cls, "NonCritical"));
    }

    /// <summary>Textual attribute match — the same name-or-qualified-name test the journey rules used inline,
    /// hoisted here so all three rules read attributes the one way (a slice is syntactic, not semantic).</summary>
    public static bool HasAttribute(ClassDeclarationSyntax cls, string name) =>
        cls.AttributeLists
            .SelectMany(list => list.Attributes)
            .Select(attr => attr.Name.ToString())
            .Any(n => n == name || n == name + "Attribute"
                   || n.EndsWith("." + name) || n.EndsWith("." + name + "Attribute"));
}
