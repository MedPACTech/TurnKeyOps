using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ICreditAccountingService
    {
        Task<TenantCreditBalanceDto> EnsureTenantBalanceAsync(
            Guid tenantId,
            DateTime usagePeriodStartUtc,
            DateTime usagePeriodEndUtc,
            bool? softCapAlertEnabled = null,
            CancellationToken ct = default);

        Task<UserCreditPeriodDto> GrantIncludedCreditsAsync(
            Guid tenantId,
            Guid userId,
            string usagePeriodKey,
            int credits,
            int? softCapThreshold = null,
            string? sourceReference = null,
            string? description = null,
            DateTime? effectiveUtc = null,
            CancellationToken ct = default);

        Task<TenantCreditBalanceDto> AddPurchasedCreditsAsync(
            Guid tenantId,
            int credits,
            DateTime usagePeriodStartUtc,
            DateTime usagePeriodEndUtc,
            DateTime expiresAtUtc,
            string? sourceReference = null,
            string? description = null,
            DateTime? effectiveUtc = null,
            CancellationToken ct = default);

        Task<CreditConsumptionResultDto> ConsumeCreditsAsync(
            Guid tenantId,
            Guid userId,
            string usagePeriodKey,
            int credits,
            string? sourceReference = null,
            string? description = null,
            DateTime? effectiveUtc = null,
            CancellationToken ct = default);

        Task<CreditConsumptionResultDto> ConsumeCreditsDirectAsync(
            Guid tenantId,
            Guid userId,
            string usagePeriodKey,
            int credits,
            string? sourceReference = null,
            string? description = null,
            DateTime? effectiveUtc = null,
            CancellationToken ct = default);

        Task<bool> EvaluateAutoTopUpAsync(
            Guid tenantId,
            Guid? requestedByUserId = null,
            string? sourceReference = null,
            string? description = null,
            DateTime? effectiveUtc = null,
            CancellationToken ct = default);

        Task<TenantCreditBalanceDto?> ExpirePurchasedCreditsAsync(
            Guid tenantId,
            DateTime? effectiveUtc = null,
            string? sourceReference = null,
            string? description = null,
            CancellationToken ct = default);
    }
}
