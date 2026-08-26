using MedInsights.API.Configurations;
using Microsoft.Extensions.Configuration;

namespace MedInsights.Authorization.Tests;

public sealed class ProductionIdentityConfigurationTests
{
    [Fact]
    public void ProductionRejectsMissingOrDevelopmentIdentityConfiguration()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["IBeam:Identity:Jwt:Issuer"] = "medinsights-api",
            ["IBeam:Identity:Jwt:Audience"] = "your-audience",
            ["IBeam:Identity:Jwt:SigningKey"] = "",
            ["IBeam:Identity:AzureTable:StorageConnectionString"] = "UseDevelopmentStorage=true",
            ["Cors:AllowedOrigins:0"] = "http://localhost:5173"
        });

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionIdentityConfiguration.Validate(configuration, "Production"));

        Assert.Contains("JWT audience", exception.Message);
        Assert.Contains("JWT signing key", exception.Message);
        Assert.Contains("production identity storage", exception.Message);
        Assert.Contains("HTTPS CORS", exception.Message);
    }

    [Fact]
    public void ProductionRejectsExplicitDevelopmentOtpBypass()
    {
        var values = ValidProductionValues();
        values["IBeam:Identity:EnableDevelopmentOtpBypass"] = "true";

        var exception = Assert.Throws<InvalidOperationException>(() =>
            ProductionIdentityConfiguration.Validate(Configuration(values), "Production"));

        Assert.Contains("development OTP bypass", exception.Message);
    }

    [Fact]
    public void ProductionAcceptsExplicitSecureIdentityConfiguration()
    {
        ProductionIdentityConfiguration.Validate(
            Configuration(ValidProductionValues()),
            "Production");
    }

    [Fact]
    public void NonProductionDoesNotRequireProductionSecrets()
    {
        ProductionIdentityConfiguration.Validate(Configuration([]), "Development");
    }

    private static Dictionary<string, string?> ValidProductionValues() => new()
    {
        ["IBeam:Identity:Jwt:Issuer"] = "https://identity.turnkeyops.example",
        ["IBeam:Identity:Jwt:Audience"] = "turnkeyops-api",
        ["IBeam:Identity:Jwt:SigningKey"] = "0123456789abcdef0123456789abcdef",
        ["IBeam:Identity:AzureTable:StorageConnectionString"] =
            "DefaultEndpointsProtocol=https;AccountName=prodidentity;AccountKey=not-a-real-test-key;EndpointSuffix=core.windows.net",
        ["Cors:AllowedOrigins:0"] = "https://app.turnkeyops.example"
    };

    private static IConfiguration Configuration(IEnumerable<KeyValuePair<string, string?>> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
