using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IBillingAdminService
    {
        Task<BillingSummaryDto> GetSummaryAsync(CancellationToken ct = default);
        Task<IReadOnlyList<BillingLedgerDto>> GetBillingLedgerAsync(int take = 100, CancellationToken ct = default);
        Task<IReadOnlyList<CreditLedgerDto>> GetCreditLedgerAsync(int take = 100, CancellationToken ct = default);
        Task<IReadOnlyList<UserCreditPeriodDto>> GetCreditPeriodsAsync(int take = 100, CancellationToken ct = default);
        Task<IReadOnlyList<TenantMembershipDto>> GetTenantUsersAsync(CancellationToken ct = default);
        Task<IReadOnlyList<InviteDto>> GetInvitesAsync(CancellationToken ct = default);
        Task<IReadOnlyList<AuditEventDto>> GetAuditEventsAsync(int take = 100, CancellationToken ct = default);
        Task<IReadOnlyList<OperationalAlertDto>> GetOperationalAlertsAsync(string? status = null, int take = 100, CancellationToken ct = default);
        Task<OperationalAlertDto> AcknowledgeOperationalAlertAsync(Guid id, CancellationToken ct = default);
        Task<TenantSeatEntitlementDto?> GetSeatViewAsync(CancellationToken ct = default);
        Task<InviteRepairReportDto> ReconcileInviteStateAsync(bool apply = false, CancellationToken ct = default);
        Task<TenantCreditBalanceDto?> GetCreditViewAsync(CancellationToken ct = default);
        Task<TenantBillingAccountDto?> GetTopUpSettingsAsync(CancellationToken ct = default);
        Task<TenantBillingAccountDto> UpdateTopUpSettingsAsync(TenantBillingAccountDto dto, CancellationToken ct = default);
    }
}
