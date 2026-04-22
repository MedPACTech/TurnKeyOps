using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MedInsights.API.Infrastructure;

public static class JwtDebugDependencyInjection
{
    public static IServiceCollection AddJwtDebugging(this IServiceCollection services)
    {
        // Debug/compat behavior for Swagger and pasted tokens.
        services.PostConfigureAll<JwtBearerOptions>(options =>
        {
            options.IncludeErrorDetails = true;

            var existingEvents = options.Events ?? new JwtBearerEvents();
            var existingOnMessageReceived = existingEvents.OnMessageReceived;
            var existingOnAuthenticationFailed = existingEvents.OnAuthenticationFailed;
            var existingOnTokenValidated = existingEvents.OnTokenValidated;

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = async context =>
                {
                    if (existingOnMessageReceived is not null)
                    {
                        await existingOnMessageReceived(context);
                    }

                    if (!string.IsNullOrWhiteSpace(context.Token))
                    {
                        return;
                    }

                    var authorization = context.Request.Headers.Authorization.ToString().Trim();
                    if (string.IsNullOrWhiteSpace(authorization))
                    {
                        return;
                    }

                    string token;
                    if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        token = authorization["Bearer ".Length..].Trim();
                        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                        {
                            token = token["Bearer ".Length..].Trim();
                        }
                    }
                    else
                    {
                        token = authorization;
                    }

                    token = token.Trim('"', '\'');
                    token = Regex.Replace(token, "\\s+", string.Empty);

                    // Extract a bare JWT if extra characters were pasted.
                    var jwtMatch = Regex.Match(token, @"[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+");
                    if (jwtMatch.Success)
                    {
                        token = jwtMatch.Value;
                    }

                    context.Token = token;
                },
                OnAuthenticationFailed = async context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("JwtAuth");
                    logger.LogError(context.Exception, "JWT authentication failed.");

                    if (existingOnAuthenticationFailed is not null)
                    {
                        await existingOnAuthenticationFailed(context);
                    }
                },
                OnTokenValidated = async context =>
                {
                    if (existingOnTokenValidated is not null)
                    {
                        await existingOnTokenValidated(context);
                    }

                    try
                    {
                        var identity = context.Principal?.Identity as ClaimsIdentity;
                        if (identity is null)
                        {
                            return;
                        }

                        var uid = FirstScalar(identity.FindFirst("uid")?.Value);
                        var sub = FirstScalar(identity.FindFirst("sub")?.Value);
                        var tid = FirstScalar(identity.FindFirst("tid")?.Value);
                        var sid = FirstScalar(identity.FindFirst("sid")?.Value);
                        var roles = ExpandStrings(identity.FindFirst("role")?.Value);

                        ReplaceSingle(identity, "uid", uid);
                        ReplaceSingle(identity, "sub", sub);
                        ReplaceSingle(identity, "tid", tid);
                        ReplaceSingle(identity, "sid", sid);

                        foreach (var claim in identity.FindAll("role").ToList())
                        {
                            identity.RemoveClaim(claim);
                        }
                        foreach (var claim in identity.FindAll(ClaimTypes.Role).ToList())
                        {
                            identity.RemoveClaim(claim);
                        }
                        foreach (var role in roles)
                        {
                            identity.AddClaim(new Claim("role", role));
                            identity.AddClaim(new Claim(ClaimTypes.Role, role));
                        }

                        var hasSub = !string.IsNullOrWhiteSpace(identity.FindFirst("sub")?.Value);
                        var hasNameId = !string.IsNullOrWhiteSpace(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value);
                        if (!string.IsNullOrWhiteSpace(uid))
                        {
                            if (!hasSub)
                            {
                                identity.AddClaim(new Claim("sub", uid));
                            }
                            if (!hasNameId)
                            {
                                identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, uid));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILoggerFactory>()
                            .CreateLogger("JwtAuth");
                        logger.LogWarning(ex, "JWT claim normalization failed; continuing with original claims.");
                    }
                }
            };
        });

        return services;
    }

    private static void ReplaceSingle(ClaimsIdentity identity, string type, string? value)
    {
        foreach (var claim in identity.FindAll(type).ToList())
        {
            identity.RemoveClaim(claim);
        }

        if (!string.IsNullOrWhiteSpace(value))
        {
            identity.AddClaim(new Claim(type, value));
        }
    }

    private static string? FirstScalar(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        raw = raw.Trim();
        if (!raw.StartsWith("[", StringComparison.Ordinal))
        {
            return raw;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
            {
                var first = doc.RootElement[0];
                return first.ValueKind == JsonValueKind.String ? first.GetString() : first.ToString();
            }
        }
        catch
        {
            // Keep original if not valid JSON array.
        }

        return raw;
    }

    private static List<string> ExpandStrings(string? raw)
    {
        var results = new List<string>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return results;
        }

        raw = raw.Trim();
        if (!raw.StartsWith("[", StringComparison.Ordinal))
        {
            results.Add(raw);
            return results;
        }

        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    var value = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        results.Add(value!);
                    }
                }
            }
        }
        catch
        {
            results.Add(raw);
        }

        return results;
    }
}
