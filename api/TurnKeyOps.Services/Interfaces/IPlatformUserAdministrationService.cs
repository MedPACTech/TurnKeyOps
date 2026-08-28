using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces;

public interface IPlatformUserAdministrationService
{
    Task<IReadOnlyList<ManagedTenantUsersDto>> GetTenantsAsync(CancellationToken ct = default);
    Task<ManagedUserInviteResultDto> CreateCustomerAdminInviteAsync(
        string tenantKey,
        CreateManagedUserInviteRequestDto request,
        CancellationToken ct = default);
}
