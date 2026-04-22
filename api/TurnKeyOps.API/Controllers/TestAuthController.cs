using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text.RegularExpressions;
using System.Security.Claims;
using System.Text;

namespace MedInsights.Controllers;

[ApiController]
[Route("api/test-auth")]
public sealed class TestAuthController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IOptionsMonitor<JwtBearerOptions> _jwtOptions;

    public TestAuthController(IConfiguration configuration, IOptionsMonitor<JwtBearerOptions> jwtOptions)
    {
        _configuration = configuration;
        _jwtOptions = jwtOptions;
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public IActionResult Public()
    {
        return Ok(new
        {
            ok = true,
            message = "public endpoint reachable"
        });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var claims = User.Claims
            .Select(c => new { c.Type, c.Value })
            .ToList();

        return Ok(new
        {
            authenticated = User.Identity?.IsAuthenticated ?? false,
            authType = User.Identity?.AuthenticationType,
            name = User.Identity?.Name,
            claims
        });
    }

    [HttpGet("me-bearer")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public IActionResult MeBearer()
    {
        var claims = User.Claims
            .Select(c => new { c.Type, c.Value })
            .ToList();

        return Ok(new
        {
            authenticated = User.Identity?.IsAuthenticated ?? false,
            authType = User.Identity?.AuthenticationType,
            name = User.Identity?.Name,
            claims
        });
    }

    [HttpGet("schemes")]
    [AllowAnonymous]
    public async Task<IActionResult> Schemes()
    {
        var provider = HttpContext.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();
        var options = HttpContext.RequestServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthenticationOptions>>()
            .Value;

        var all = await provider.GetAllSchemesAsync();

        return Ok(new
        {
            defaults = new
            {
                options.DefaultScheme,
                options.DefaultAuthenticateScheme,
                options.DefaultChallengeScheme,
                options.DefaultForbidScheme,
                options.DefaultSignInScheme,
                options.DefaultSignOutScheme
            },
            schemes = all.Select(s => new
            {
                s.Name,
                HandlerType = s.HandlerType?.FullName,
                DisplayName = s.DisplayName
            }).ToList()
        });
    }

    [HttpGet("header")]
    [Authorize] // Include bearer scheme in Swagger for diagnostics.
    [AllowAnonymous]
    public IActionResult Header()
    {
        return Ok(new
        {
            authorization = Request.Headers.Authorization.ToString(),
            hasAuthorization = Request.Headers.ContainsKey("Authorization")
        });
    }

    [HttpPost("validate")]
    [AllowAnonymous]
    public IActionResult Validate([FromBody] TokenValidateRequest request)
    {
        var issuer = _configuration["IBeam:Identity:Jwt:Issuer"];
        var audience = _configuration["IBeam:Identity:Jwt:Audience"];
        var signingKey = _configuration["IBeam:Identity:Jwt:SigningKey"];

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { ok = false, error = "Token is required." });
        }

        // Accept common pasted variants:
        // - raw JWT
        // - "Bearer <jwt>"
        // - JSON-escaped wrappers and leading/trailing junk
        var token = request.Token.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Bearer ".Length..].Trim();
        }
        token = token.Trim('"', '\'');
        token = Regex.Replace(token, "\\s+", string.Empty);

        var jwtMatch = Regex.Match(token, @"[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+");
        if (jwtMatch.Success)
        {
            token = jwtMatch.Value;
        }

        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return BadRequest(new
            {
                ok = false,
                error = "Token must contain exactly 3 JWT segments.",
                segmentCount = parts.Length,
                tokenLength = token.Length,
                tokenPreview = token.Length > 24 ? token[..24] + "..." : token
            });
        }

        if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience) || string.IsNullOrWhiteSpace(signingKey))
        {
            return BadRequest(new
            {
                ok = false,
                error = "Missing JWT settings under IBeam:Identity:Jwt (Issuer/Audience/SigningKey).",
                issuer,
                audience,
                hasSigningKey = !string.IsNullOrWhiteSpace(signingKey)
            });
        }

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(2),
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role
                },
                out var validatedToken);

            return Ok(new
            {
                ok = true,
                tokenType = validatedToken.GetType().Name,
                claims = principal.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }
        catch (Exception ex)
        {
            return Unauthorized(new
            {
                ok = false,
                errorType = ex.GetType().Name,
                error = ex.Message,
                segmentCount = parts.Length,
                headerLength = parts[0].Length,
                payloadLength = parts[1].Length,
                signatureLength = parts[2].Length,
                tokenPreview = token.Length > 24 ? token[..24] + "..." : token
            });
        }
    }

    [HttpPost("validate-runtime")]
    [AllowAnonymous]
    public IActionResult ValidateRuntime([FromBody] TokenValidateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new { ok = false, error = "Token is required." });
        }

        var token = request.Token.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Bearer ".Length..].Trim();
        }
        token = token.Trim('"', '\'');
        token = Regex.Replace(token, "\\s+", string.Empty);

        var jwtMatch = Regex.Match(token, @"[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+\.[A-Za-z0-9\-_]+");
        if (jwtMatch.Success)
        {
            token = jwtMatch.Value;
        }

        var options = _jwtOptions.Get(JwtBearerDefaults.AuthenticationScheme);
        var tvp = options.TokenValidationParameters?.Clone() ?? new TokenValidationParameters();

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, tvp, out var validatedToken);

            return Ok(new
            {
                ok = true,
                tokenType = validatedToken.GetType().Name,
                options = new
                {
                    options.Authority,
                    options.Audience,
                    tvp.ValidateIssuer,
                    tvp.ValidIssuer,
                    tvp.ValidateAudience,
                    tvp.ValidAudience,
                    tvp.ValidateIssuerSigningKey,
                    HasSigningKey = tvp.IssuerSigningKey is not null
                },
                claims = principal.Claims.Select(c => new { c.Type, c.Value }).ToList()
            });
        }
        catch (Exception ex)
        {
            return Unauthorized(new
            {
                ok = false,
                errorType = ex.GetType().Name,
                error = ex.Message,
                options = new
                {
                    options.Authority,
                    options.Audience,
                    tvp.ValidateIssuer,
                    tvp.ValidIssuer,
                    tvp.ValidateAudience,
                    tvp.ValidAudience,
                    tvp.ValidateIssuerSigningKey,
                    HasSigningKey = tvp.IssuerSigningKey is not null
                }
            });
        }
    }

    public sealed class TokenValidateRequest
    {
        public string Token { get; set; } = string.Empty;
    }
}
