using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IReferralWorkItemService
    {
        Task<IReadOnlyList<ReferralWorkItemDto>> GetAllAsync(
            Guid? patientId = null,
            Guid? encounterId = null,
            string? status = null,
            string? search = null,
            CancellationToken ct = default);

        Task<ReferralWorkItemDto?> GetAsync(Guid id, CancellationToken ct = default);
        Task<ReferralWorkItemDto> CreateAsync(CreateReferralWorkItemDto dto, CancellationToken ct = default);
        Task<ReferralWorkItemDto> UpdateAsync(Guid id, UpdateReferralWorkItemDto dto, CancellationToken ct = default);
        Task<ReferralWorkItemDto> UpdateWorkflowAsync(Guid id, UpdateReferralWorkflowDto dto, CancellationToken ct = default);
        Task<ReferralWorkItemDto> AddActionAsync(Guid id, ReferralWorkItemActionDto dto, CancellationToken ct = default);
        Task<ReferralWorkItemDto> RefreshCaseSummaryAsync(Guid id, bool forceRefresh = false, CancellationToken ct = default);
    }
}
