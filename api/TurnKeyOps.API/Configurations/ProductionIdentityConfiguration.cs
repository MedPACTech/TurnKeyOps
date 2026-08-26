namespace MedInsights.API.Configurations;

public static class ProductionIdentityConfiguration
{
    public static void Validate(IConfiguration configuration, string environmentName)
    {
        if (!string.Equals(environmentName, Environments.Production, StringComparison.OrdinalIgnoreCase))
            return;

        var issuer = Read(configuration, "Jwt:Issuer");
        var audience = Read(configuration, "Jwt:Audience");
        var signingKey = Read(configuration, "Jwt:SigningKey");
        var identityStorage = Read(configuration, "AzureTable:StorageConnectionString");
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(issuer) || IsPlaceholder(issuer)) failures.Add("JWT issuer");
        if (string.IsNullOrWhiteSpace(audience) || IsPlaceholder(audience)) failures.Add("JWT audience");
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32 || IsPlaceholder(signingKey))
            failures.Add("JWT signing key (minimum 32 characters)");
        if (string.IsNullOrWhiteSpace(identityStorage) ||
            identityStorage.Contains("UseDevelopmentStorage", StringComparison.OrdinalIgnoreCase))
            failures.Add("production identity storage");
        if (allowedOrigins.Length == 0 || allowedOrigins.Any(origin =>
                !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps ||
                origin.Contains('*', StringComparison.Ordinal)))
            failures.Add("explicit HTTPS CORS origins");
        if (configuration.GetValue<bool>("IBeam:Identity:EnableDevelopmentOtpBypass"))
            failures.Add("development OTP bypass must be disabled");

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Production identity configuration is invalid: {string.Join(", ", failures)}.");
        }
    }

    private static string? Read(IConfiguration configuration, string suffix) =>
        configuration[$"IBeam:Identity:{suffix}"]?.Trim()
        ?? configuration[$"Identity:{suffix}"]?.Trim();

    private static bool IsPlaceholder(string value) =>
        value.Contains("your-", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("<required>", StringComparison.OrdinalIgnoreCase) ||
        value.Contains("changeme", StringComparison.OrdinalIgnoreCase);
}
