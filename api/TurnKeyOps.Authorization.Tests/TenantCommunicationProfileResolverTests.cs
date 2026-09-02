using MedInsights.Lib.Configurations;
using MedInsights.Services;
using Microsoft.Extensions.Options;

namespace MedInsights.Authorization.Tests;

public sealed class TenantCommunicationProfileResolverTests
{
    [Fact]
    public void ResolvesOnlyTheRequestedTenantProfile()
    {
        var bdrId = Guid.NewGuid();
        var thinkPinkId = Guid.NewGuid();
        var resolver = new TenantCommunicationProfileResolver(Options.Create(
            new ProductionCommunicationOptions
            {
                Tenants = new Dictionary<string, TenantCommunicationProfile>
                {
                    ["bdr"] = new()
                    {
                        TenantId = bdrId,
                        EmailFromAddress = "noreply@bdr.example",
                        SmsFromPhoneNumber = "+16145550101"
                    },
                    ["thinkpink"] = new()
                    {
                        TenantId = thinkPinkId,
                        EmailFromAddress = "noreply@thinkpink.example",
                        SmsFromPhoneNumber = "+16145550102"
                    }
                }
            }));

        var bdr = resolver.Resolve(bdrId);
        var thinkPink = resolver.Resolve(thinkPinkId);

        Assert.Equal("noreply@bdr.example", bdr.EmailFromAddress);
        Assert.Equal("noreply@thinkpink.example", thinkPink.EmailFromAddress);
        Assert.NotEqual(bdr.SmsFromPhoneNumber, thinkPink.SmsFromPhoneNumber);
    }

    [Fact]
    public void RejectsUnknownTenantInsteadOfFallingBackAcrossBrands()
    {
        var resolver = new TenantCommunicationProfileResolver(
            Options.Create(new ProductionCommunicationOptions()));

        Assert.Throws<InvalidOperationException>(() => resolver.Resolve(Guid.NewGuid()));
    }
}
