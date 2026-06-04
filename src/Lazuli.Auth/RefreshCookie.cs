using Microsoft.AspNetCore.Http;

namespace Lazuli.Auth;

/// <summary>The app-specific knobs of the refresh cookie: its <paramref name="Name"/> and the
/// <paramref name="Path"/> it is scoped to (typically the auth routes, so it is sent nowhere else). The
/// security attributes are <em>not</em> here — httpOnly / Secure / SameSite=Strict are the framework's
/// opinion, not a per-app choice.</summary>
public sealed record RefreshCookieOptions(string Name, string Path = "/");

/// <summary>How the refresh token reaches each client. Web stores it in an httpOnly, Secure,
/// SameSite=Strict cookie — invisible to JS, so an XSS payload cannot exfiltrate it; the access token
/// still rides in the body for the Authorization header. Mobile / API clients get the refresh in the body
/// and keep it in secure storage. A request opts into web delivery with the <c>X-Client: web</c> header —
/// that one signal is why the same endpoints serve both web and mobile.</summary>
public sealed class RefreshCookie(RefreshCookieOptions options)
{
    // The header a web client sends to opt into cookie delivery. A framework convention, not app config.
    private const string ClientHeader = "X-Client";
    private const string WebClient = "web";

    /// <summary>True when the caller is the web client, and so wants cookie-based refresh.</summary>
    public bool IsWeb(HttpRequest req) =>
        string.Equals(req.Headers[ClientHeader], WebClient, StringComparison.OrdinalIgnoreCase);

    /// <summary>The refresh token to act on: the httpOnly cookie for web, the request body otherwise.</summary>
    public string RefreshFrom(HttpRequest req, string? fromBody) =>
        req.Cookies.TryGetValue(options.Name, out var fromCookie) && fromCookie.Length > 0 ? fromCookie : fromBody ?? "";

    /// <summary>Plant the web refresh cookie, scoped to the auth routes and the token's lifetime.</summary>
    public void SetRefresh(HttpResponse res, string token, DateTimeOffset expires) =>
        res.Cookies.Append(options.Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = expires,
            Path = options.Path,
        });

    /// <summary>Drop the web refresh cookie (logout). Harmless when no cookie was set.</summary>
    public void Clear(HttpResponse res) =>
        res.Cookies.Delete(options.Name, new CookieOptions { Path = options.Path });
}
