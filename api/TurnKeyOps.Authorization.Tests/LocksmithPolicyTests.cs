using System.Text.Json;
using TurnKeyOps.Services;
namespace MedInsights.Authorization.Tests;

public sealed class LocksmithPolicyTests
{
    private static readonly Guid MembershipId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"locksmith\":null}")]
    [InlineData("invalid")]
    public void MissingOrMalformedCapabilitiesFailClosed(string? json)
        => Assert.Empty(LocksmithPolicy.CapabilitiesFor(json, MembershipId));

    [Fact]
    public void CapabilitiesAreScopedToMembershipAndOnlyKnownTypesAreReturned()
    {
        var json = JsonSerializer.Serialize(new { locksmith = new { techCapabilities = new Dictionary<string, string[]> {
            [MembershipId.ToString()] = ["residential", "commercial", "residential", "unknown"],
            [Guid.NewGuid().ToString()] = ["commercial"]
        } } });
        Assert.Equal(new[] { "residential", "commercial" }, LocksmithPolicy.CapabilitiesFor(json, MembershipId));
        Assert.Empty(LocksmithPolicy.CapabilitiesFor(json, Guid.NewGuid()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void RejectsInvalidDiscounts(decimal discount)
    {
        var settings = JsonSerializer.SerializeToElement(new {
            techCapabilities = new Dictionary<string, string[]>(),
            pricing = new { laborRatePerHour = (decimal?)null, approvalAboveTotal = (decimal?)null, maxDiscountPercent = discount, requireOfficeApproval = true },
            selfBookingEnabled = false
        });
        Assert.Throws<ArgumentException>(() => LocksmithPolicy.Validate(settings));
    }

    [Fact]
    public void AcceptsUnsetRatesAndBothCapabilities()
    {
        var settings = JsonSerializer.SerializeToElement(new {
            techCapabilities = new Dictionary<string, string[]> { [MembershipId.ToString()] = ["residential", "commercial"] },
            pricing = new { laborRatePerHour = (decimal?)null, approvalAboveTotal = (decimal?)null, maxDiscountPercent = 0, requireOfficeApproval = true },
            selfBookingEnabled = false
        });
        LocksmithPolicy.Validate(settings);
    }
}
