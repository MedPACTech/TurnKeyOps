using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class UserCreditPeriodMapper
    {
        public static UserCreditPeriodDto ToDto(UserCreditPeriod entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UserId = entity.UserId,
            PeriodKey = entity.PeriodKey,
            IncludedCreditsGranted = entity.IncludedCreditsGranted,
            IncludedCreditsConsumed = entity.IncludedCreditsConsumed,
            PurchasedCreditsConsumed = entity.PurchasedCreditsConsumed,
            SoftCapThreshold = entity.SoftCapThreshold,
            SoftCapAlertSentUtc = entity.SoftCapAlertSentUtc,
            DateCreated = entity.DateCreated,
            DateUpdated = entity.DateUpdated
        };

        public static UserCreditPeriod ToEntity(UserCreditPeriodDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            TenantId = dto.TenantId,
            UserId = dto.UserId,
            PeriodKey = dto.PeriodKey.Trim(),
            IncludedCreditsGranted = dto.IncludedCreditsGranted,
            IncludedCreditsConsumed = dto.IncludedCreditsConsumed,
            PurchasedCreditsConsumed = dto.PurchasedCreditsConsumed,
            SoftCapThreshold = dto.SoftCapThreshold,
            SoftCapAlertSentUtc = dto.SoftCapAlertSentUtc,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            DateUpdated = dto.DateUpdated,
            IsDeleted = false
        };
    }
}
