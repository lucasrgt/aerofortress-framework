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

    /// <summary>Record an error for <paramref name="field"/> unless <paramref name="ok"/> holds, with a stable
    /// <paramref name="code"/> the client localizes from and a developer <paramref name="message"/>. Fluent.</summary>
    public Validation Check(bool ok, string field, string code, string message)
    {
        if (!ok)
            _fields.Add(new FieldError(field, code, message));
        return this;
    }

    /// <summary>Record an error for <paramref name="field"/> directly, with its <paramref name="code"/> and a
    /// developer <paramref name="message"/>. Fluent.</summary>
    public Validation Add(string field, string code, string message)
    {
        _fields.Add(new FieldError(field, code, message));
        return this;
    }

    /// <summary>
    /// Fold a value object's construction into the accumulation: if <paramref name="result"/>
    /// failed, record its error under <paramref name="field"/> — inheriting the value object's own
    /// <see cref="Error.Code"/> and message, since the value object is the single source of the rule
    /// (and its code). Fluent.
    /// </summary>
    public Validation Collect<T>(string field, Result<T> result)
    {
        if (result.IsFailure)
            _fields.Add(new FieldError(field, result.Error.Code, result.Error.Message));
        return this;
    }

    /// <summary>Whether any field error has been recorded.</summary>
    public bool Failed => _fields.Count > 0;

    /// <summary>Build one validation <see cref="Error"/> carrying every recorded field error.</summary>
    public Error ToError() => Error.Validation(_fields);
}
