using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ITenantSeatEntitlementService
    {
        Task<TenantSeatEntitlementDto?> GetCurrentAsync(CancellationToken ct = default);
        Task<TenantSeatEntitlementDto> SyncPurchasedSeatsAsync(Guid tenantId, Guid subscriptionId, int purchasedSeats, CancellationToken ct = default);
        Task<TenantSeatEntitlementDto> ScheduleSeatReductionAsync(Guid tenantId, int requestedSeatCount, CancellationToken ct = default);
        Task<TenantSeatEntitlementDto> ReserveSeatAsync(Guid tenantId, CancellationToken ct = default);
        Task<TenantSeatEntitlementDto> AssignSeatAsync(Guid tenantId, CancellationToken ct = default);
        Task<TenantSeatEntitlementDto> ReleaseSeatAsync(Guid tenantId, string previousSeatStatus, CancellationToken ct = default);
        Task<TenantSeatEntitlementDto> ApplyRenewalAsync(Guid tenantId, CancellationToken ct = default);
    }
}
