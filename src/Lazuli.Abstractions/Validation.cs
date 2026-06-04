using System.Collections.Generic;

namespace Lazuli.Abstractions;

/// <summary>
/// Accumulates field-level validation errors so a slice reports all of them at once instead of
/// failing on the first. Append with <see cref="Check"/> (a condition) or <see cref="Add"/>
/// (e.g. a value object's failed construction), then return <see cref="ToError"/> while <see
/// cref="Failed"/>. The value object stays the single source of each rule — this only collects
/// the failures. Removable: a slice could accumulate into a plain list just as well.
/// </summary>
public sealed class Validation
{
    private readonly List<FieldError> _fields = [];

    /// <summary>Record an error for <paramref name="field"/> unless <paramref name="ok"/> holds. Fluent.</summary>
    public Validation Check(bool ok, string field, string message)
    {
        if (!ok)
            _fields.Add(new FieldError(field, message));
        return this;
    }

    /// <summary>Record an error for <paramref name="field"/> directly — e.g. a value object's failure. Fluent.</summary>
    public Validation Add(string field, string message)
    {
        _fields.Add(new FieldError(field, message));
        return this;
    }

    /// <summary>
    /// Fold a value object's construction into the accumulation: if <paramref name="result"/>
    /// failed, record its message under <paramref name="field"/>. Keeps the value object the
    /// single source of the rule while collecting its failure alongside the others. Fluent.
    /// </summary>
    public Validation Collect<T>(string field, Result<T> result)
    {
        if (result.IsFailure)
            _fields.Add(new FieldError(field, result.Error.Message));
        return this;
    }

    /// <summary>Whether any field error has been recorded.</summary>
    public bool Failed => _fields.Count > 0;

    /// <summary>Build one validation <see cref="Error"/> carrying every recorded field error.</summary>
    public Error ToError() => Error.Validation(_fields);
}
