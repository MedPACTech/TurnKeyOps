using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ITenantBillingAccountService
    {
        Task<TenantBillingAccountDto?> GetCurrentAsync(CancellationToken ct = default);
        Task<TenantBillingAccountDto> UpsertAsync(TenantBillingAccountDto dto, CancellationToken ct = default);
    }
}
