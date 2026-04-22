using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class TenantSubscriptionMapper
    {
        public static TenantSubscriptionDto ToDto(TenantSubscription entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            Provider = entity.Provider,
            PlanCode = entity.PlanCode,
            BillingCadence = entity.BillingCadence,
            SubscriptionStatus = entity.SubscriptionStatus,
            ProviderSubscriptionId = entity.ProviderSubscriptionId,
            PricingRuleSnapshotId = entity.PricingRuleSnapshotId,
            CurrentSeatCount = entity.CurrentSeatCount,
            NextRenewalSeatCount = entity.NextRenewalSeatCount,
            CancelAtTermEnd = entity.CancelAtTermEnd,
            TermStartUtc = entity.TermStartUtc,
            TermEndUtc = entity.TermEndUtc,
            DateCreated = entity.DateCreated,
            DateUpdated = entity.DateUpdated
        };

        public static TenantSubscription ToEntity(TenantSubscriptionDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            TenantId = dto.TenantId,
            Provider = dto.Provider.Trim(),
            PlanCode = dto.PlanCode.Trim(),
            BillingCadence = dto.BillingCadence.Trim(),
            SubscriptionStatus = dto.SubscriptionStatus.Trim(),
            ProviderSubscriptionId = Normalize(dto.ProviderSubscriptionId),
            PricingRuleSnapshotId = dto.PricingRuleSnapshotId,
            CurrentSeatCount = dto.CurrentSeatCount,
            NextRenewalSeatCount = dto.NextRenewalSeatCount,
            CancelAtTermEnd = dto.CancelAtTermEnd,
            TermStartUtc = dto.TermStartUtc,
            TermEndUtc = dto.TermEndUtc,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = dto.DateUpdated,
            IsDeleted = false
        };

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
