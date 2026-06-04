using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Lazuli.Auth;

/// <summary>Wires the access-token mechanism end to end in one call: the minter (<see cref="IAccessTokens"/>),
/// the per-request reader (<see cref="ICurrentUser"/> via <see cref="HttpCurrentUser"/>), and the JwtBearer
/// validator — all from the same secret / issuer / audience. The agreement between minting and validating
/// (a token this app signs is one it accepts) used to be a hand-copied foot-gun across two code sites; here
/// it is structural, because both read the same three arguments.</summary>
public static class JwtAccessTokenExtensions
{
    /// <summary>Register JWT access-token minting + validation + the <see cref="ICurrentUser"/> reader.</summary>
    /// <example>
    /// In <c>Program.cs</c>, before <c>builder.Build()</c>:
    /// <code>
    /// builder.Services.AddJwtAccessTokens(jwtSecret, issuer: "myapp", audience: "myapp");
    /// </code>
    /// Then the usual <c>app.UseAuthentication(); app.UseAuthorization();</c> in the pipeline.
    /// </example>
    public static IServiceCollection AddJwtAccessTokens(this IServiceCollection services, string secret, string issuer, string audience)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();
        services.AddSingleton<IAccessTokens>(sp => new AccessTokens(secret, issuer, audience, sp.GetRequiredService<TimeProvider>()));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;   // else "sub" is remapped to NameIdentifier and the claims break
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };
            });

        return services;
    }

    /// <summary>Register the web refresh-cookie delivery service with the app's cookie name and path.</summary>
    /// <example>
    /// <code>
    /// builder.Services.AddRefreshCookie("myapp_refresh", path: "/account");
    /// </code>
    /// A slice's route then takes <c>RefreshCookie cookies</c> by DI and calls
    /// <c>cookies.IsWeb / RefreshFrom / SetRefresh / Clear</c>.
    /// </example>
    public static IServiceCollection AddRefreshCookie(this IServiceCollection services, string name, string path = "/")
    {
        services.AddSingleton(new RefreshCookieOptions(name, path));
        services.AddSingleton<RefreshCookie>();
        return services;
    }
}
