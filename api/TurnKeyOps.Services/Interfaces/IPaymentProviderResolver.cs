namespace MedInsights.Services.Interfaces
{
    public interface IPaymentProviderResolver
    {
        IPaymentProvider GetDefaultProvider();
        IPaymentProvider GetRequiredProvider(string providerName);
        Task<IPaymentProvider> ResolveForTenantAsync(Guid? tenantId, string? providerName, CancellationToken ct = default);
        Task<IPaymentProvider> ResolveForSubscriptionAsync(string subscriptionId, string? providerName, CancellationToken ct = default);
        Task<IPaymentProvider> ResolveForCustomerAsync(Guid? tenantId, string customerId, string? providerName, CancellationToken ct = default);
    }
}
