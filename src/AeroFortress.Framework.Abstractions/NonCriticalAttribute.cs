namespace AeroFortress.Framework.Abstractions;

/// <summary>
/// Marks a <c>[Slice]</c> as a deliberately <em>non</em>-critical operation — the positive, reviewable
/// counterpart to <see cref="CriticalAttribute"/>. It states, in code a reviewer can see and challenge,
/// "this slice's failure is cheap; it needs no journeys." Like <see cref="CriticalAttribute"/> it is a
/// pure marker — no runtime behavior.
///
/// It earns its keep only under a stricter criticality policy (the <c>[testing] criticality</c> dial in
/// <c>AeroFortress.toml</c>). Under <c>"explicit"</c> the doctor (<c>LZ0029</c>) requires every slice to carry
/// either <see cref="CriticalAttribute"/> or this marker, so no slice's criticality is left undecided.
/// Under <c>"strict"</c> an unmarked slice is treated as critical, and this marker is the one explicit,
/// auditable opt-out. Under the default <c>"opt-in"</c> policy it is inert — there is nothing to downgrade.
///
/// Prefer a forced, reviewable downgrade over a silent absence: a slice that simply lacks <c>[Critical]</c>
/// could be a considered decision or an oversight, and nothing tells them apart. <c>[NonCritical]</c> makes
/// the decision visible the way <see cref="CriticalAttribute"/> makes the obligation visible.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class NonCriticalAttribute : Attribute;
