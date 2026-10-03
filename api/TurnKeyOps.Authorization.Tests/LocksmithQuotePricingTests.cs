using System.Text.Json;
using Azure;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Entities;
using TurnKeyOps.Services;

namespace MedInsights.Authorization.Tests;
public sealed class LocksmithQuotePricingTests
{
    internal static readonly Guid MembershipId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    internal static TenantSettingsDocument Settings(bool approval = false, bool sample = false, string[]? capabilities = null, decimal? tax = 10, decimal standardHours = 4) => new()
    {
        ETag = new ETag("policy-v1"), ValuesJson = JsonSerializer.Serialize(new { locksmith = new {
            techCapabilities = new Dictionary<string, string[]> { [MembershipId.ToString()] = capabilities ?? ["residential"] },
            selfBookingEnabled = false,
            pricing = new { laborRatePerHour = 100m, maxDiscountPercent = 5m, approvalAboveTotal = 1000m, requireOfficeApproval = approval, taxPercent = tax,
                laborHoursByJobType = new Dictionary<string, Dictionary<string, decimal>> { ["residential"] = new() { ["Door and frame replacement"] = standardHours } } },
            catalog = new[] { new { id = "lock-1", name = "Configured lock", jobTypes = new[] { "residential" }, unitPrice = 80m, sample } }
        } })
    };
    internal static LocksmithQuoteInputDto Input(decimal discount = 0) => new() { JobType = "residential", LaborHours = 1, DiscountPercent = discount,
        Items = [new() { CatalogItemId = "lock-1", Quantity = 2, OpeningName = "Front" }],
        Openings = [new() { Id = "opening-1", Name = "Front", Measurements = new() { ["width"] = new() { Value = "36", Certainty = "measured" } } }]
    };
    [Fact]
    public void PolicyVersionUsesPersistedTimestampWhenEtagIsMissing()
    {
        var document = Settings(); document.ETag = default; document.DateUpdated = DateTime.UtcNow;
        var first = LocksmithQuotePricing.Context(document, ["residential"]).PolicyVersion;
        Assert.NotEmpty(first);
        document.DateUpdated = document.DateUpdated.AddSeconds(1);
        Assert.NotEqual(first, LocksmithQuotePricing.Context(document, ["residential"]).PolicyVersion);
    }
    [Fact]
    public void UsesServerCatalogRatesTaxAndSavedPolicyVersion()
    {
        var result = LocksmithQuotePricing.Calculate(Settings(), Input(5));
        Assert.Equal(260m, result.Subtotal);
        Assert.Equal(13m, result.DiscountAmount);
        Assert.Equal(24.7m, result.TaxAmount);
        Assert.Equal(271.7m, result.Total);
        Assert.Equal("policy-v1", result.PolicyVersion);
        Assert.Equal("36", result.Openings[0].Measurements["width"].Value);
        Assert.Empty(result.ApprovalReasons);
    }
    [Fact]
    public void ReturnsGuardrailReasonsInsteadOfSilentlyReducingDiscount()
    {
        var result = LocksmithQuotePricing.Calculate(Settings(approval: true), Input(10));
        Assert.Equal(10m, result.DiscountPercent);
        Assert.Equal(2, result.ApprovalReasons.Count);
        Assert.True(result.RequiresOfficeApproval);
        Assert.Null(result.OfficeApprovedAtUtc);
    }
    [Fact]
    public void BlocksSampleCatalogMissingTaxAndWrongJobType()
    {
        Assert.Throws<ArgumentException>(() => LocksmithQuotePricing.Calculate(Settings(sample: true), Input()));
        Assert.Throws<ArgumentException>(() => LocksmithQuotePricing.Calculate(Settings(tax: null), Input()));
        var input = Input(); input.JobType = "commercial";
        Assert.Throws<ArgumentException>(() => LocksmithQuotePricing.Calculate(Settings(), input));
    }
    [Fact]
    public void EnforcesAmountThresholdAndOpeningReferences()
    {
        var input = Input(); input.Items[0].Quantity = 20;
        Assert.Contains("The quote total exceeds the office approval threshold.", LocksmithQuotePricing.Calculate(Settings(), input).ApprovalReasons);
        input.Items[0].OpeningName = "Unknown";
        Assert.Throws<ArgumentException>(() => LocksmithQuotePricing.Calculate(Settings(), input));
    }
    [Fact]
    public void AppliesStandardHoursAndRecordsAnExplainedIncrease()
    {
        var input = Input(); input.Openings[0].Service = "Door and frame replacement"; input.LaborHours = 0;
        var standard = LocksmithQuotePricing.Calculate(Settings(), input);
        Assert.Equal(4m, standard.StandardLaborHours);
        Assert.Equal(4m, standard.LaborHours);
        Assert.Equal(560m, standard.Subtotal);
        input.LaborHours = 6;
        Assert.Throws<ArgumentException>(() => LocksmithQuotePricing.Calculate(Settings(), input));
        input.LaborOverrideReason = "Damaged jamb requires extra preparation.";
        var increased = LocksmithQuotePricing.Calculate(Settings(), input);
        Assert.Equal(6m, increased.LaborHours);
        Assert.Equal(input.LaborOverrideReason, increased.LaborOverrideReason);
        input.LaborHours = 3;
        Assert.Throws<ArgumentException>(() => LocksmithQuotePricing.Calculate(Settings(), input));
        input.LaborHours = 0; input.LaborOverrideReason = null;
        Assert.Equal(5m, LocksmithQuotePricing.Calculate(Settings(standardHours: 5), input).LaborHours);
    }
}
