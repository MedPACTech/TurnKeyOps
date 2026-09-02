using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace MedInsights.API.Infrastructure;

public sealed class TurnKeyAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly ILogger<TurnKeyAuthorizationResultHandler> _logger;

    public TurnKeyAuthorizationResultHandler(ILogger<TurnKeyAuthorizationResultHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        var challenged = authorizeResult.Challenged || context.User.Identity?.IsAuthenticated != true;
        var statusCode = challenged ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden;
        var code = challenged ? "Unauthorized" : "Forbidden";

        _logger.LogWarning(
            "Authorization denied. StatusCode={StatusCode} Method={Method} Path={Path} TraceId={TraceId} Authenticated={Authenticated}",
            statusCode,
            context.Request.Method,
            context.Request.Path,
            context.TraceIdentifier,
            context.User.Identity?.IsAuthenticated == true);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        if (challenged)
            context.Response.Headers.WWWAuthenticate = "Bearer";

        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            new
            {
                success = false,
                data = (object?)null,
                errors = new[] { new { code, message = code } },
                traceId = context.TraceIdentifier
            },
            cancellationToken: context.RequestAborted);
    }
}
