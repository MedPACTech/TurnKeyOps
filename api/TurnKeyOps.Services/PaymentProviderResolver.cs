using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.Services
{
    public sealed class PaymentProviderResolver : IPaymentProviderResolver
    {
        private readonly IReadOnlyDictionary<string, IPaymentProvider> _providers;
        private readonly ITenantBillingAccountRepository _billingAccountRepository;
        private readonly ITenantSubscriptionRepository _subscriptionRepository;
        private readonly BillingIntegrationOptions _options;

        public PaymentProviderResolver(
            IEnumerable<IPaymentProvider> providers,
            ITenantBillingAccountRepository billingAccountRepository,
            ITenantSubscriptionRepository subscriptionRepository,
            IOptions<BillingIntegrationOptions> options)
        {
            _providers = providers.ToDictionary(x => x.ProviderName, StringComparer.OrdinalIgnoreCase);
            _billingAccountRepository = billingAccountRepository;
            _subscriptionRepository = subscriptionRepository;
            _options = options.Value;
        }

        public IPaymentProvider GetDefaultProvider()
        {
            EnsureBillingEnabled();

            if (string.IsNullOrWhiteSpace(_options.DefaultProvider))
                throw new InvalidOperationException("No default billing provider is configured.");

            return GetRequiredProvider(_options.DefaultProvider);
        }

        public IPaymentProvider GetRequiredProvider(string providerName)
        {
            EnsureBillingEnabled();

            if (string.IsNullOrWhiteSpace(providerName))
                throw new ArgumentException("ProviderName is required.", nameof(providerName));

            var normalized = providerName.Trim();
            if (!_options.EnabledProviders.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Payment provider '{normalized}' is disabled.");

            if (_providers.TryGetValue(normalized, out var provider))
                return provider;

            throw new InvalidOperationException($"Payment provider '{normalized}' is not registered.");
        }

        public async Task<IPaymentProvider> ResolveForTenantAsync(Guid? tenantId, string? providerName, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(providerName))
                return GetRequiredProvider(providerName);

            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                var account = await _billingAccountRepository.GetAsync(EntityKeyPolicy.TenantPartition(tenantId.Value), "BILLING", ct);
                if (account is not null && !string.IsNullOrWhiteSpace(account.Provider))
                    return GetRequiredProvider(account.Provider);
            }

            return GetDefaultProvider();
        }

        public async Task<IPaymentProvider> ResolveForSubscriptionAsync(string subscriptionId, string? providerName, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(providerName))
                return GetRequiredProvider(providerName);

            if (string.IsNullOrWhiteSpace(subscriptionId))
                return GetDefaultProvider();

            foreach (var provider in _providers.Values)
            {
                var subscription = await _subscriptionRepository.GetByProviderSubscriptionIdAsync(provider.ProviderName, subscriptionId, ct);
                if (subscription is not null)
                    return provider;
            }

            return GetDefaultProvider();
        }

        public async Task<IPaymentProvider> ResolveForCustomerAsync(Guid? tenantId, string customerId, string? providerName, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(providerName))
                return GetRequiredProvider(providerName);

            if (!string.IsNullOrWhiteSpace(customerId))
            {
                foreach (var provider in _providers.Values)
                {
                    var account = await _billingAccountRepository.GetByProviderCustomerIdAsync(provider.ProviderName, customerId, ct);
                    if (account is not null)
                        return provider;
                }
            }

            return await ResolveForTenantAsync(tenantId, null, ct);
        }

        private void EnsureBillingEnabled()
        {
            if (!_options.Enabled)
                throw new InvalidOperationException("Production billing is disabled.");
        }
    }
}
