using MedInsights.Lib.Configurations;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services;
using Microsoft.Extensions.Options;
using Moq;

namespace MedInsights.Authorization.Tests;

public sealed class PaymentProviderResolverTests
{
    [Fact]
    public void DisabledBillingFailsClosedBeforeResolvingAProvider()
    {
        var resolver = new PaymentProviderResolver(
            [],
            Mock.Of<ITenantBillingAccountRepository>(),
            Mock.Of<ITenantSubscriptionRepository>(),
            Options.Create(new BillingIntegrationOptions { Enabled = false }));

        var exception = Assert.Throws<InvalidOperationException>(() => resolver.GetDefaultProvider());

        Assert.Equal("Production billing is disabled.", exception.Message);
    }
}
