using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class CreditLedgerMapper
    {
        public static CreditLedgerDto ToDto(CreditLedger entity) => new()
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            UserId = entity.UserId,
            LedgerType = entity.LedgerType,
            SourceBucket = entity.SourceBucket,
            Amount = entity.Amount,
            BalanceAfter = entity.BalanceAfter,
            UsagePeriodKey = entity.UsagePeriodKey,
            ExpiresAtUtc = entity.ExpiresAtUtc,
            SourceReference = entity.SourceReference,
            Description = entity.Description,
            EffectiveUtc = entity.EffectiveUtc
        };

        public static CreditLedger ToEntity(CreditLedgerDto dto, string partitionKey, string rowKey) => new()
        {
            Id = dto.Id,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            TenantId = dto.TenantId,
            UserId = dto.UserId,
            LedgerType = dto.LedgerType.Trim(),
            SourceBucket = Normalize(dto.SourceBucket),
            Amount = dto.Amount,
            BalanceAfter = dto.BalanceAfter,
            UsagePeriodKey = Normalize(dto.UsagePeriodKey),
            ExpiresAtUtc = dto.ExpiresAtUtc,
            SourceReference = Normalize(dto.SourceReference),
            Description = Normalize(dto.Description),
            EffectiveUtc = dto.EffectiveUtc,
            IsDeleted = false
        };

        private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
