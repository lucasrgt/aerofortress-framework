namespace AeroFortress.Framework.Abstractions;

/// <summary>
/// Declares that a <c>[Slice]</c> (or a method within one) must be PROVEN against a named AVP
/// acceptance criterion: there must exist an <c>[AVP("&lt;id&gt;")]</c> verification for the same id.
/// The doctor (<c>LZ0030</c>) reads this marker and fails the build when the proof is missing — the
/// obligation is enforced at compile time, not left to a checklist.
///
/// AVP (the Acceptance Verification Protocol) is a separate, standalone verifier package; this marker
/// only names the criterion by its stable id. The dependency is one-way: the framework knows the id,
/// never the other way around. Like <see cref="CriticalAttribute"/> it is a pure marker — no runtime
/// behavior; it raises the proof bar and anchors the knowledge graph.
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
