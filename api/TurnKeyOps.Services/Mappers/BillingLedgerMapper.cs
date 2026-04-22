using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class BillingLedgerMapper
    {
        public static BillingLedgerDto ToDto(BillingLedger entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Provider = entity.Provider,
            EventType = entity.EventType,
            ProviderEventId = entity.ProviderEventId,
            ProviderInvoiceId = entity.ProviderInvoiceId,
            ProviderPaymentIntentId = entity.ProviderPaymentIntentId,
            ProviderSubscriptionId = entity.ProviderSubscriptionId,
            Amount = entity.Amount,
            Currency = entity.Currency,
            Description = entity.Description,
            EffectiveUtc = entity.EffectiveUtc
        };

        public static BillingLedger ToEntity(BillingLedgerDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            TenantId = dto.TenantId,
            Provider = dto.Provider.Trim(),
            EventType = dto.EventType.Trim(),
            ProviderEventId = Normalize(dto.ProviderEventId),
            ProviderInvoiceId = Normalize(dto.ProviderInvoiceId),
            ProviderPaymentIntentId = Normalize(dto.ProviderPaymentIntentId),
            ProviderSubscriptionId = Normalize(dto.ProviderSubscriptionId),
            Amount = dto.Amount,
            Currency = string.IsNullOrWhiteSpace(dto.Currency) ? "USD" : dto.Currency.Trim().ToUpperInvariant(),
            Description = Normalize(dto.Description),
            EffectiveUtc = dto.EffectiveUtc,
            IsDeleted = false
        };

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
