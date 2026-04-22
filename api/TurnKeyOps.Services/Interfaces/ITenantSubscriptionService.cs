using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ITenantSubscriptionService
    {
        Task<IEnumerable<TenantSubscriptionDto>> GetAllAsync(CancellationToken ct = default);
        Task<TenantSubscriptionDto?> GetAsync(Guid id, CancellationToken ct = default);
        Task<TenantSubscriptionDto> UpsertAsync(TenantSubscriptionDto dto, CancellationToken ct = default);
    }
}
