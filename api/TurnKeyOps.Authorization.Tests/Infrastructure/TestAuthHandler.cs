using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedInsights.Authorization.Tests.Infrastructure;

internal sealed class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";

    public TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var userId = Request.Headers["X-Test-UserId"].FirstOrDefault();
        var tenantId = Request.Headers["X-Test-TenantId"].FirstOrDefault();
        var role = Request.Headers["X-Test-Role"].FirstOrDefault();
        var roleId = Request.Headers["X-Test-RoleId"].FirstOrDefault();

        if (!Guid.TryParse(userId, out var parsedUserId) || !Guid.TryParse(tenantId, out var parsedTenantId))
            return Task.FromResult(AuthenticateResult.Fail("Missing test identity headers."));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, parsedUserId.ToString("D")),
            new("uid", parsedUserId.ToString("D")),
            new("tenant_id", parsedTenantId.ToString("D")),
            new("tenant", parsedTenantId.ToString("D")),
            new(ClaimTypes.Name, "integration-user")
        };

        if (!string.IsNullOrWhiteSpace(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        if (!string.IsNullOrWhiteSpace(roleId))
        {
            claims.Add(new Claim("rid", roleId));
            claims.Add(new Claim("role_id", roleId));
        }

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
