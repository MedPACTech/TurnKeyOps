using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface ITenantBillingAccountRepository : IBaseRepositoryAsync<TenantBillingAccount>
    {
        Task<TenantBillingAccount?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<TenantBillingAccount?> GetByProviderCustomerIdAsync(string provider, string providerCustomerId, CancellationToken ct = default);
    }
}
