using Lazuli.Abstractions;

namespace Lazuli.Identity;

/// <summary>
/// A dev/test <see cref="IExternalIdentity"/> that treats the token as the email — so OAuth flows run
/// without a real provider — reporting the provider as "fake". An empty token is rejected. A real
/// verifier, validating the id_token against a provider's JWKS, is an external plugin.
/// </summary>
public sealed class FakeExternalIdentity : IExternalIdentity
{
    /// <inheritdoc />
    public Result<ExternalUser> Verify(string idToken) =>
        string.IsNullOrWhiteSpace(idToken)
            ? Error.Unauthorized("invalid identity token")
            : new ExternalUser("fake", idToken, idToken);
}
