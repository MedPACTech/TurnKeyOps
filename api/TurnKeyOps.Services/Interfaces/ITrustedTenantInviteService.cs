using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces;

/// <summary>
/// Creates an invite for a tenant selected by a trusted platform service.
/// This interface must never be injected directly into a tenant-facing controller.
/// </summary>
public interface ITrustedTenantInviteService
{
    Task<InviteDto> CreateForTenantAsync(
        Guid tenantId,
        CreateInviteRequestDto dto,
        CancellationToken ct = default);
}
