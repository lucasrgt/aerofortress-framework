using System.Collections.Generic;

namespace Lazuli.Abstractions;

/// <summary>
/// The category of a failure. It maps one-to-one to an HTTP status at the boundary and is a
/// closed catalog: the compiler guarantees an <see cref="Error"/> only ever carries a known
/// kind, and the HTTP mapping stays exhaustive. App-specific failures specialise a kind via
/// the message and fields — they do not extend this enum.
/// </summary>
public enum ErrorKind
{
    /// <summary>The input failed validation (400).</summary>
    Validation,

    /// <summary>Authentication is required or failed (401).</summary>
    Unauthorized,

    /// <summary>Authenticated, but not allowed (403).</summary>
    Forbidden,

    /// <summary>A referenced thing does not exist (404).</summary>
    NotFound,

    /// <summary>The action conflicts with current state (409).</summary>
    Conflict,

    /// <summary>A business rule was violated (422).</summary>
    BusinessRule,

    /// <summary>Too many requests; the caller is rate-limited (429).</summary>
    RateLimit,

    /// <summary>An unexpected internal failure (500).</summary>
    Internal,

    /// <summary>A dependency is unavailable (503).</summary>
    Unavailable,
}

/// <summary>One field-level validation error: which input <paramref name="Field"/> failed, and why.</summary>
/// <param name="Field">The offending input field (e.g. <c>amount</c>).</param>
/// <param name="Message">A human-readable explanation for that field.</param>
public readonly record struct FieldError(string Field, string Message);

/// <summary>
/// A structured failure: a machine-readable <paramref name="Kind"/> (drives HTTP mapping, the
/// knowledge graph, i18n), a human <paramref name="Message"/>, and — for validation — the list
/// of <paramref name="Fields"/> that failed, so a slice reports them all at once. Slices return
/// these instead of throwing for expected, domain-level outcomes.
/// </summary>
/// <param name="Kind">The failure category, from the closed <see cref="ErrorKind"/> catalog.</param>
/// <param name="Message">A human-readable explanation of what went wrong.</param>
/// <param name="Fields">The field-level errors, when the failure is a validation of several inputs.</param>
public readonly record struct Error(
    ErrorKind Kind,
    string Message,
    IReadOnlyList<FieldError>? Fields = null)
{
    /// <summary>The input failed validation, with a single human message.</summary>
    public static Error Validation(string message) => new(ErrorKind.Validation, message);

    /// <summary>The input failed validation across one or more <paramref name="fields"/>.</summary>
    public static Error Validation(IReadOnlyList<FieldError> fields) =>
        new(ErrorKind.Validation, "Validation failed", fields);

    /// <summary>A referenced thing does not exist.</summary>
    public static Error NotFound(string message) => new(ErrorKind.NotFound, message);

    /// <summary>The action conflicts with current state.</summary>
    public static Error Conflict(string message) => new(ErrorKind.Conflict, message);

    /// <summary>A business rule was violated.</summary>
    public static Error BusinessRule(string message) => new(ErrorKind.BusinessRule, message);

    /// <summary>The caller is rate-limited.</summary>
    public static Error RateLimit(string message) => new(ErrorKind.RateLimit, message);

    /// <summary>Authentication is required or failed (e.g. wrong credentials).</summary>
    public static Error Unauthorized(string message) => new(ErrorKind.Unauthorized, message);

    /// <summary>Authenticated, but not allowed to perform this action.</summary>
    public static Error Forbidden(string message) => new(ErrorKind.Forbidden, message);
}

/// <summary>
/// The outcome of a slice handler: either a value of <typeparamref name="T"/> or an
/// <see cref="Error"/>. Handlers stay HTTP-agnostic and testable — the API boundary maps a
/// <see cref="Result{T}"/> to an HTTP response, not the handler itself.
/// </summary>
/// <typeparam name="T">The value produced on success.</typeparam>
public readonly record struct Result<T>
{
    private readonly T? _value;
    private readonly Error _error;

    private Result(bool isSuccess, T? value, Error error)
    {
        IsSuccess = isSuccess;
        _value = value;
        _error = error;
    }

    /// <summary>Whether the operation succeeded.</summary>
    public bool IsSuccess { get; }

    /// <summary>Whether the operation failed; the inverse of <see cref="IsSuccess"/>.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>The success value. Throws if the result is a failure.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Result is a failure; no value to read.");

    /// <summary>The failure error. Throws if the result is a success.</summary>
    public Error Error => IsFailure
        ? _error
        : throw new InvalidOperationException("Result is a success; no error to read.");

    /// <summary>Create a success carrying <paramref name="value"/>.</summary>
    public static Result<T> Ok(T value) => new(true, value, default);

    /// <summary>Create a failure carrying <paramref name="error"/>.</summary>
    public static Result<T> Fail(Error error) => new(false, default, error);

    /// <summary>Lift a value into a success.</summary>
    public static implicit operator Result<T>(T value) => Ok(value);

    /// <summary>Lift an <see cref="Error"/> into a failure.</summary>
    public static implicit operator Result<T>(Error error) => Fail(error);
}
