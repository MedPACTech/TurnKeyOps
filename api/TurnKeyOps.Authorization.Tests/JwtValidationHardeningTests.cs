using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MedInsights.API.Configurations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace MedInsights.Authorization.Tests;

public sealed class JwtValidationHardeningTests
{
    private const string Issuer = "https://identity.turnkeyops.test";
    private const string Audience = "turnkey-api";
    private static readonly SymmetricSecurityKey SigningKey = new(Encoding.UTF8.GetBytes("test-signing-key-that-is-long-enough-for-hmac-256"));

    [Fact]
    public void RejectsWrongIssuerAndAudienceWhileAcceptingExpectedToken()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["IBeam:Identity:Jwt:Issuer"] = Issuer,
                ["IBeam:Identity:Jwt:Audience"] = Audience,
                ["IBeam:Identity:Jwt:ClockSkewSeconds"] = "0"
            })
            .Build();
        var options = new JwtBearerOptions();
        JwtValidationHardening.Apply(options, configuration);
        options.TokenValidationParameters.IssuerSigningKey = SigningKey;

        var handler = new JwtSecurityTokenHandler();
        var valid = handler.ValidateToken(Token(Issuer, Audience), options.TokenValidationParameters, out _);

        Assert.Equal("user-id", valid.FindFirstValue(ClaimTypes.NameIdentifier) ?? valid.FindFirstValue(JwtRegisteredClaimNames.Sub));
        Assert.Throws<SecurityTokenInvalidIssuerException>(
            () => handler.ValidateToken(Token("https://attacker.invalid", Audience), options.TokenValidationParameters, out _));
        Assert.Throws<SecurityTokenInvalidAudienceException>(
            () => handler.ValidateToken(Token(Issuer, "other-api"), options.TokenValidationParameters, out _));
    }

    private static string Token(string issuer, string audience)
    {
        var token = new JwtSecurityToken(
            issuer,
            audience,
            [new Claim(JwtRegisteredClaimNames.Sub, "user-id")],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(5),
            new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
