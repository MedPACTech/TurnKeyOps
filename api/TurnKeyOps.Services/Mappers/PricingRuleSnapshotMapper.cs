using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class PricingRuleSnapshotMapper
    {
        public static PricingRuleSnapshotDto ToDto(PricingRuleSnapshot entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            PlanCode = entity.PlanCode,
            SeatUnitPrice = entity.SeatUnitPrice,
            IncludedCreditsPerSeatPerMonth = entity.IncludedCreditsPerSeatPerMonth,
            CadenceDiscountPercent = entity.CadenceDiscountPercent,
            PromoType = entity.PromoType,
            PromoValue = entity.PromoValue,
            PromoStartUtc = entity.PromoStartUtc,
            PromoEndUtc = entity.PromoEndUtc,
            IntroOfferEndUtc = entity.IntroOfferEndUtc,
            DateCreated = entity.DateCreated
        };

        public static PricingRuleSnapshot ToEntity(PricingRuleSnapshotDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            TenantId = dto.TenantId,
            PlanCode = dto.PlanCode.Trim(),
            SeatUnitPrice = dto.SeatUnitPrice,
            IncludedCreditsPerSeatPerMonth = dto.IncludedCreditsPerSeatPerMonth,
            CadenceDiscountPercent = dto.CadenceDiscountPercent,
            PromoType = Normalize(dto.PromoType),
            PromoValue = dto.PromoValue,
            PromoStartUtc = dto.PromoStartUtc,
            PromoEndUtc = dto.PromoEndUtc,
            IntroOfferEndUtc = dto.IntroOfferEndUtc,
            DateCreated = dto.DateCreated ?? DateTime.UtcNow,
            IsDeleted = false
        };

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
