namespace AeroFortress.Framework.Abstractions;

/// <summary>
/// Declares that a <c>[Slice]</c> (or a method within one) must be PROVEN against a named AVP
/// acceptance criterion. This is the legacy, inline form of the acceptance obligation; it has been
/// SUPERSEDED by the per-module Clockwork spec manifest (<c>&lt;Module&gt;.spec.toml</c>), where a slice's
/// criteria are declared in a reviewable file beside the module rather than as a source attribute. The
/// doctor (<c>AF0030</c>/<c>AF0031</c>) reads the manifest, not this marker — the attribute is retained only
/// so existing code keeps compiling and is no longer enforced.
///
/// AVP (the Acceptance Verification Protocol) is a separate, standalone verifier package; this marker
/// only names the criterion by its stable id. The dependency is one-way: the framework knows the id,
/// never the other way around. Like <see cref="CriticalAttribute"/> it is a pure marker — no runtime
/// behavior.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class VerifyAttribute : Attribute
{
    /// <summary>Declares the AVP criterion this code must be proven against.</summary>
    /// <param name="criterionId">The stable criterion id from the AVP catalog (e.g. "own-resource-only").</param>
    public VerifyAttribute(string criterionId) => CriterionId = criterionId;

    /// <summary>The stable AVP criterion id this code must be proven against.</summary>
    public string CriterionId { get; }
}
