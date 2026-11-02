using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AdPulse.Api.Auth;

public sealed class IdentityOptions
{
    public string Authority { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public bool RequireHttpsMetadata { get; init; } = true;
    public string SigningKeyForTestsOnly { get; init; } = string.Empty;

    public void ValidateProviderConfiguration()
    {
        if (!Uri.TryCreate(Authority, UriKind.Absolute, out var authority)
            || authority is null
            || string.IsNullOrEmpty(authority.Host)
            || authority.UserInfo.Length > 0
            || (authority.Scheme != Uri.UriSchemeHttps
                && (RequireHttpsMetadata || authority.Scheme != Uri.UriSchemeHttp)))
        {
            throw new InvalidOperationException("Identity:Authority must be an absolute HTTP(S) issuer URL and use HTTPS when required.");
        }

        if (string.IsNullOrWhiteSpace(Audience) || Audience.Length > 200)
        {
            throw new InvalidOperationException("Identity:Audience is required.");
        }
    }

    public TokenValidationParameters CreateTestValidationParameters()
    {
        if (string.IsNullOrWhiteSpace(Issuer)
            || string.IsNullOrWhiteSpace(Audience)
            || Audience.Length > 200
            || string.IsNullOrWhiteSpace(SigningKeyForTestsOnly)
            || Encoding.UTF8.GetByteCount(SigningKeyForTestsOnly) < 32)
        {
            throw new InvalidOperationException("Issuer, audience and a non-whitespace test signing key of at least 32 UTF-8 bytes are required for local token tests.");
        }

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = Issuer,
            ValidateAudience = true,
            ValidAudience = Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKeyForTestsOnly)),
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero
        };
    }
}
