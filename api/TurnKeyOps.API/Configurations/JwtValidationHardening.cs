using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace MedInsights.API.Configurations;

public static class JwtValidationHardening
{
    public static void Apply(JwtBearerOptions options, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        var issuer = Read(configuration, "Issuer");
        var audience = Read(configuration, "Audience");
        var clockSkewSeconds = configuration.GetValue<int?>("IBeam:Identity:Jwt:ClockSkewSeconds")
            ?? configuration.GetValue<int?>("Identity:Jwt:ClockSkewSeconds")
            ?? 60;

        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ClockSkew = TimeSpan.FromSeconds(Math.Clamp(clockSkewSeconds, 0, 300));

        if (!string.IsNullOrWhiteSpace(issuer))
            options.TokenValidationParameters.ValidIssuer = issuer;
        if (!string.IsNullOrWhiteSpace(audience))
            options.TokenValidationParameters.ValidAudience = audience;
    }

    private static string? Read(IConfiguration configuration, string key)
        => configuration[$"IBeam:Identity:Jwt:{key}"]?.Trim()
           ?? configuration[$"Identity:Jwt:{key}"]?.Trim();
}
