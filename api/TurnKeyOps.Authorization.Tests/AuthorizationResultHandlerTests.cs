using System.Text.Json;
using MedInsights.API.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedInsights.Authorization.Tests;

public sealed class AuthorizationResultHandlerTests
{
    [Theory]
    [InlineData(true, StatusCodes.Status401Unauthorized, "Unauthorized")]
    [InlineData(false, StatusCodes.Status403Forbidden, "Forbidden")]
    public async Task WritesConsistentSafeAuthorizationResponse(bool challenged, int expectedStatus, string expectedCode)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.TraceIdentifier = "trace-authorization-test";
        if (!challenged)
            context.User = new System.Security.Claims.ClaimsPrincipal(
                new System.Security.Claims.ClaimsIdentity([], "test"));
        var handler = new TurnKeyAuthorizationResultHandler(NullLogger<TurnKeyAuthorizationResultHandler>.Instance);
        var result = challenged ? PolicyAuthorizationResult.Challenge() : PolicyAuthorizationResult.Forbid();
        var nextCalled = false;

        await handler.HandleAsync(
            _ => { nextCalled = true; return Task.CompletedTask; },
            context,
            new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build(),
            result);

        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.False(nextCalled);
        Assert.Equal(expectedStatus, context.Response.StatusCode);
        Assert.Equal(expectedCode, body.RootElement.GetProperty("errors")[0].GetProperty("code").GetString());
        Assert.Equal("trace-authorization-test", body.RootElement.GetProperty("traceId").GetString());
        Assert.DoesNotContain("claim", body.RootElement.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
