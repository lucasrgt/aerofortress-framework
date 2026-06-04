using Lazuli.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Lazuli.AspNetCore;

/// <summary>
/// Maps a domain <see cref="Result{T}"/> to an HTTP response at the route boundary, so slice
/// handlers never reference <c>IResult</c> and stay unit-testable without a web host. The
/// <see cref="ErrorKind"/> drives the status — one mapping, identical for every Lazuli app, so
/// the framework ships it instead of each app re-deriving it.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>Render the result: the value on success, or the error (kind, message, details) with its mapped status.</summary>
    public static IResult ToHttp<T>(this Result<T> result) =>
        result.IsSuccess
            ? Results.Ok(result.Value)
            : Results.Json(
                new { error = result.Error.Kind.ToString(), result.Error.Message, result.Error.Fields },
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
