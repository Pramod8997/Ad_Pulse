using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AdPulse.Api.Auth;

public sealed class IdentityTokenValidator(IdentityOptions options)
{
    private readonly IdentityOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public ClaimsPrincipal ValidateForTests(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new ArgumentException("A bearer token is required.", nameof(token));
        }

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(token, _options.CreateTestValidationParameters(), out _);
    }
}
