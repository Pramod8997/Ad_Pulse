using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AdPulse.Api.Auth;
using Microsoft.IdentityModel.Tokens;

namespace AdPulse.Api.Tests.ReleaseGates;

public sealed class AuthenticationReleaseGateTests
{
    private const string Issuer = "https://issuer.example.test/";
    private const string Audience = "adpulse-api";
    private const string SigningKey = "phase-one-test-signing-key-32-chars!";

    [Fact]
    public void Rejects_a_missing_bearer_token()
    {
        var validator = new IdentityTokenValidator(Options());

        Assert.Throws<ArgumentException>(() => validator.ValidateForTests(string.Empty));
    }

    [Fact]
    public void Rejects_a_malformed_bearer_token()
    {
        var validator = new IdentityTokenValidator(Options());

        Assert.Throws<SecurityTokenMalformedException>(() => validator.ValidateForTests("not-a-jwt"));
    }

    [Fact]
    public void Rejects_wrong_issuer_and_audience()
    {
        var validator = new IdentityTokenValidator(Options());

        Assert.Throws<SecurityTokenInvalidIssuerException>(() =>
            validator.ValidateForTests(Token("https://other.example.test/", Audience, DateTime.UtcNow.AddMinutes(5))));
        Assert.Throws<SecurityTokenInvalidAudienceException>(() =>
            validator.ValidateForTests(Token(Issuer, "other-api", DateTime.UtcNow.AddMinutes(5))));
    }

    [Fact]
    public void Rejects_expired_and_non_expiring_tokens()
    {
        var validator = new IdentityTokenValidator(Options());

        Assert.Throws<SecurityTokenExpiredException>(() =>
            validator.ValidateForTests(Token(Issuer, Audience, DateTime.UtcNow.AddMinutes(-5))));
        Assert.Throws<SecurityTokenNoExpirationException>(() =>
            validator.ValidateForTests(Token(Issuer, Audience, null)));
    }

    [Fact]
    public void Rejects_invalid_and_unsigned_signatures()
    {
        var validator = new IdentityTokenValidator(Options());

        Assert.ThrowsAny<SecurityTokenException>(() =>
            validator.ValidateForTests(Token(
                Issuer,
                Audience,
                DateTime.UtcNow.AddMinutes(5),
                "a-different-test-signing-key-that-is-long-enough")));
        Assert.ThrowsAny<SecurityTokenException>(() =>
            validator.ValidateForTests(Token(Issuer, Audience, DateTime.UtcNow.AddMinutes(5), null)));
    }

    [Theory]
    [InlineData("ftp://issuer.example.test/")]
    [InlineData("not-an-absolute-url")]
    [InlineData("https://user:password@issuer.example.test/")]
    public void Rejects_unsafe_authority_configuration(string authority)
    {
        var options = new IdentityOptions
        {
            Authority = authority,
            Audience = Audience,
            RequireHttpsMetadata = false
        };

        Assert.Throws<InvalidOperationException>(() => options.ValidateProviderConfiguration());
    }

    private static IdentityOptions Options() => new()
    {
        Issuer = Issuer,
        Audience = Audience,
        SigningKeyForTestsOnly = SigningKey
    };

    private static string Token(string issuer, string audience, DateTime? expires, string? signingKey = SigningKey)
    {
        var credentials = signingKey is null
            ? null
            : new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [
                new Claim("sub", "user-1"),
                new Claim("tenant_id", "tenant-a")
            ],
            expires?.AddMinutes(-10),
            expires,
            credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
