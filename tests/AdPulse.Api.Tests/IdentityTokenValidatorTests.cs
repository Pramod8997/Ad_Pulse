using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AdPulse.Api.Auth;
using Microsoft.IdentityModel.Tokens;

namespace AdPulse.Api.Tests;

public sealed class IdentityTokenValidatorTests
{
    private const string Issuer = "https://issuer.example.test/";
    private const string Audience = "adpulse-api";
    private const string Key = "phase-one-test-signing-key-32-chars!";

    [Fact]
    public void Accepts_valid_issuer_audience_and_unexpired_token()
    {
        var validator = new IdentityTokenValidator(Options());
        var principal = validator.ValidateForTests(Token(Issuer, Audience, DateTime.UtcNow.AddMinutes(5)));

        Assert.Equal("user-1", principal.FindFirstValue("sub"));
        Assert.Equal("tenant-a", principal.FindFirstValue("tenant_id"));
    }

    [Fact]
    public void Rejects_wrong_issuer()
    {
        var validator = new IdentityTokenValidator(Options());

        Assert.Throws<SecurityTokenInvalidIssuerException>(() => validator.ValidateForTests(Token("https://other.example.test/", Audience, DateTime.UtcNow.AddMinutes(5))));
    }

    [Fact]
    public void Rejects_wrong_audience()
    {
        var validator = new IdentityTokenValidator(Options());

        Assert.Throws<SecurityTokenInvalidAudienceException>(() => validator.ValidateForTests(Token(Issuer, "other-api", DateTime.UtcNow.AddMinutes(5))));
    }

    [Fact]
    public void Rejects_expired_token_with_zero_clock_skew()
    {
        var validator = new IdentityTokenValidator(Options());

        Assert.Throws<SecurityTokenExpiredException>(() => validator.ValidateForTests(Token(Issuer, Audience, DateTime.UtcNow.AddMinutes(-5))));
    }

    [Fact]
    public void Rejects_token_without_expiration()
    {
        var validator = new IdentityTokenValidator(Options());
        var token = Token(Issuer, Audience, null);
        Assert.False(new JwtSecurityTokenHandler().ReadJwtToken(token).Payload.Expiration.HasValue);

        Assert.Throws<SecurityTokenNoExpirationException>(() => validator.ValidateForTests(token));
    }

    [Fact]
    public void Rejects_invalid_signature()
    {
        var validator = new IdentityTokenValidator(Options());

        Assert.ThrowsAny<SecurityTokenException>(() => validator.ValidateForTests(
            Token(Issuer, Audience, DateTime.UtcNow.AddMinutes(5), "a-different-test-signing-key-that-is-long-enough")));
    }

    [Fact]
    public void Rejects_unsigned_token()
    {
        var validator = new IdentityTokenValidator(Options());

        Assert.ThrowsAny<SecurityTokenException>(() => validator.ValidateForTests(
            Token(Issuer, Audience, DateTime.UtcNow.AddMinutes(5), null)));
    }

    [Fact]
    public void Rejects_http_authority_when_https_metadata_is_required()
    {
        var options = new IdentityOptions
        {
            Authority = "http://issuer.example.test/",
            Audience = Audience
        };

        Assert.Throws<InvalidOperationException>(() => options.ValidateProviderConfiguration());
    }

    [Fact]
    public void Allows_http_authority_only_when_https_metadata_is_not_required()
    {
        var options = new IdentityOptions
        {
            Authority = "http://issuer.example.test/",
            Audience = Audience,
            RequireHttpsMetadata = false
        };

        options.ValidateProviderConfiguration();
    }

    [Theory]
    [InlineData("ftp://issuer.example.test/")]
    [InlineData("not-an-absolute-url")]
    [InlineData("https://user:password@issuer.example.test/")]
    public void Rejects_unsafe_authority(string authority)
    {
        var options = new IdentityOptions
        {
            Authority = authority,
            Audience = Audience,
            RequireHttpsMetadata = false
        };

        Assert.Throws<InvalidOperationException>(() => options.ValidateProviderConfiguration());
    }

    [Fact]
    public void Rejects_weak_test_signing_key()
    {
        var options = new IdentityOptions
        {
            Issuer = Issuer,
            Audience = Audience,
            SigningKeyForTestsOnly = new string(' ', 32)
        };

        Assert.Throws<InvalidOperationException>(() => options.CreateTestValidationParameters());
    }

    private static IdentityOptions Options() => new()
    {
        Issuer = Issuer,
        Audience = Audience,
        SigningKeyForTestsOnly = Key
    };

    private static string Token(string issuer, string audience, DateTime? expires, string? signingKey = Key)
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
