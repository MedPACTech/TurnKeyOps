using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface ITenantSettingsService
{
    Task<TenantSettingsDocumentDto> GetPublicAsync(Guid tenantId, CancellationToken ct = default);
    Task<TenantSettingsDocumentDto> GetProtectedAsync(string kind, CancellationToken ct = default);
    Task<TenantSettingsDocumentDto> UpsertAsync(
        string kind,
        UpdateTenantSettingsDocumentDto input,
        CancellationToken ct = default);
}
