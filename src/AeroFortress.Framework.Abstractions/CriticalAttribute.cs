namespace AeroFortress.Framework.Abstractions;

/// <summary>
/// Marks a <c>[Slice]</c> as a high-stakes operation — one where a failure costs money or trust (a
/// deposit, a transfer, a checkout). The doctor (<c>AF0008</c>) then requires the slice to be proven
/// end-to-end on both paths: a happy journey and at least one sad journey. Like <see cref="SliceAttribute"/>
/// it is a pure marker — no runtime behavior; it only raises the test bar and anchors the knowledge graph.
///
/// Use it sparingly. If half your slices are critical, the marker has lost its meaning — mark the few
/// operations whose failure is expensive, not every write.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CriticalAttribute : Attribute;
