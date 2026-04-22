using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ITenantCreditBalanceService
    {
        Task<TenantCreditBalanceDto?> GetCurrentAsync(CancellationToken ct = default);
        Task<TenantCreditBalanceDto> UpsertAsync(TenantCreditBalanceDto dto, CancellationToken ct = default);
    }
}
