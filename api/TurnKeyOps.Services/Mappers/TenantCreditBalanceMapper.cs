using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class TenantCreditBalanceMapper
    {
        public static TenantCreditBalanceDto ToDto(TenantCreditBalance entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            CurrentUsagePeriodStartUtc = entity.CurrentUsagePeriodStartUtc,
            CurrentUsagePeriodEndUtc = entity.CurrentUsagePeriodEndUtc,
            PurchasedCreditsAvailable = entity.PurchasedCreditsAvailable,
            PurchasedCreditsExpireAtUtc = entity.PurchasedCreditsExpireAtUtc,
            SoftCapAlertEnabled = entity.SoftCapAlertEnabled,
            LastTopUpUtc = entity.LastTopUpUtc,
            TopUpsThisCycle = entity.TopUpsThisCycle,
            DateCreated = entity.DateCreated,
            DateUpdated = entity.DateUpdated
        };

        public static TenantCreditBalance ToEntity(TenantCreditBalanceDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            TenantId = dto.TenantId,
            CurrentUsagePeriodStartUtc = dto.CurrentUsagePeriodStartUtc,
            CurrentUsagePeriodEndUtc = dto.CurrentUsagePeriodEndUtc,
            PurchasedCreditsAvailable = dto.PurchasedCreditsAvailable,
            PurchasedCreditsExpireAtUtc = dto.PurchasedCreditsExpireAtUtc,
            SoftCapAlertEnabled = dto.SoftCapAlertEnabled,
            LastTopUpUtc = dto.LastTopUpUtc,
            TopUpsThisCycle = dto.TopUpsThisCycle,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = dto.DateUpdated,
            IsDeleted = false
        };
    }
}
