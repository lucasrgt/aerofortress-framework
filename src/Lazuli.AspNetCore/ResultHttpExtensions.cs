using Lazuli.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Lazuli.AspNetCore;

/// <summary>
/// The wire shape of a failed <see cref="Result{T}"/> — the error envelope every endpoint returns on the sad
/// path. A named type (not an anonymous object) so it lands in the OpenAPI document and the generated client is
/// typed on the sad path too. The shape is unchanged from the original anonymous body
/// (<c>error</c>/<c>message</c>/<c>fields</c>), so the wire is stable.
/// </summary>
/// <param name="Error">The <see cref="ErrorKind"/> name (e.g. <c>NotFound</c>).</param>
/// <param name="Message">The human-readable message.</param>
/// <param name="Fields">The field-level errors when the failure is a multi-field validation; otherwise null.</param>
public sealed record ErrorBody(string Error, string Message, IReadOnlyList<FieldError>? Fields);

/// <summary>
/// Maps a domain <see cref="Result{T}"/> to an HTTP response at the route boundary, so slice handlers never
/// reference <c>IResult</c> and stay unit-testable without a web host. The <see cref="ErrorKind"/> drives the
/// status — one mapping, identical for every Lazuli app, so the framework ships it instead of each app
/// re-deriving it.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>Render the result as a <em>typed</em> response — <see cref="Ok{T}"/> with the value on success,
    /// or an <see cref="ErrorBody"/> with its mapped status on failure. The return is a typed
    /// <see cref="Results{T1,T2}"/> union (not bare <c>IResult</c>) so ASP.NET's OpenAPI infers both the 200
    /// schema (<typeparamref name="T"/>) and the error schema — which is what makes the generated client typed
    /// end to end, the foundation of the "wired, not mocked" frontend harness.</summary>
    public static Results<Ok<T>, JsonHttpResult<ErrorBody>> ToHttp<T>(this Result<T> result) =>
        result.IsSuccess
            ? TypedResults.Ok(result.Value)
            : TypedResults.Json(
                new ErrorBody(result.Error.Kind.ToString(), result.Error.Message, result.Error.Fields),
                statusCode: StatusFor(result.Error.Kind));

    // Exhaustive over ErrorKind: add a kind without a case here and the compiler flags it.
    private static int StatusFor(ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.BusinessRule => StatusCodes.Status422UnprocessableEntity,
        ErrorKind.RateLimit => StatusCodes.Status429TooManyRequests,
        ErrorKind.Internal => StatusCodes.Status500InternalServerError,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
    };
}
