using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Lazuli.Auth;

/// <summary>Issues 15-minute HMAC-SHA256 access tokens. The <paramref name="secret"/>, <paramref
/// name="issuer"/>, and <paramref name="audience"/> are the same values the JwtBearer validator is
/// configured with (see <see cref="JwtAccessTokenExtensions.AddJwtAccessTokens"/>), so the tokens this
/// mints are exactly what the middleware accepts. Issuer and audience are parameters, not constants — the
/// framework names no app.</summary>
public sealed class AccessTokens(string secret, string issuer, string audience, TimeProvider clock) : IAccessTokens
{
    /// <inheritdoc />
    public string Issue(Guid userId, Guid orgId, string? role, Guid sessionId, string? name)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Expires = clock.GetUtcNow().UtcDateTime.AddMinutes(15),
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", userId.ToString()),
                new Claim("org", orgId.ToString()),
                new Claim("role", role ?? string.Empty),
                new Claim("sid", sessionId.ToString()),
                new Claim("name", name ?? string.Empty),
            ]),
        };
        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
