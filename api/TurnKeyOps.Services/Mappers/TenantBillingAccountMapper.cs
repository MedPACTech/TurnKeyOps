using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class TenantBillingAccountMapper
    {
        public static TenantBillingAccountDto ToDto(TenantBillingAccount entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Provider = entity.Provider,
            BillingStatus = entity.BillingStatus,
            ProviderCustomerId = entity.ProviderCustomerId,
            DefaultPaymentMethodRef = entity.DefaultPaymentMethodRef,
            AutoTopUpEnabled = entity.AutoTopUpEnabled,
            TopUpPackSku = entity.TopUpPackSku,
            TopUpTriggerThreshold = entity.TopUpTriggerThreshold,
            MaxTopUpsPerCycle = entity.MaxTopUpsPerCycle,
            MaxTopUpSpendPerCycle = entity.MaxTopUpSpendPerCycle,
            LastAutoTopUpAttemptUtc = entity.LastAutoTopUpAttemptUtc,
            LastAutoTopUpSuccessUtc = entity.LastAutoTopUpSuccessUtc,
            LastAutoTopUpFailureUtc = entity.LastAutoTopUpFailureUtc,
            AutoTopUpFailureCount = entity.AutoTopUpFailureCount,
            LastAutoTopUpError = entity.LastAutoTopUpError,
            DateCreated = entity.DateCreated,
            DateUpdated = entity.DateUpdated
        };

        public static TenantBillingAccount ToEntity(TenantBillingAccountDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            TenantId = dto.TenantId,
            Provider = dto.Provider.Trim(),
            BillingStatus = dto.BillingStatus.Trim(),
            ProviderCustomerId = Normalize(dto.ProviderCustomerId),
            DefaultPaymentMethodRef = Normalize(dto.DefaultPaymentMethodRef),
            AutoTopUpEnabled = dto.AutoTopUpEnabled,
            TopUpPackSku = Normalize(dto.TopUpPackSku),
            TopUpTriggerThreshold = dto.TopUpTriggerThreshold,
            MaxTopUpsPerCycle = dto.MaxTopUpsPerCycle,
            MaxTopUpSpendPerCycle = dto.MaxTopUpSpendPerCycle,
            LastAutoTopUpAttemptUtc = dto.LastAutoTopUpAttemptUtc,
            LastAutoTopUpSuccessUtc = dto.LastAutoTopUpSuccessUtc,
            LastAutoTopUpFailureUtc = dto.LastAutoTopUpFailureUtc,
            AutoTopUpFailureCount = dto.AutoTopUpFailureCount,
            LastAutoTopUpError = Normalize(dto.LastAutoTopUpError),
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = dto.DateUpdated,
            IsDeleted = false
        };

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
