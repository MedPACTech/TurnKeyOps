using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IFacilityService
    {
        Task<IReadOnlyList<FacilityDto>> GetAllAsync(CancellationToken ct = default);
        Task<FacilityDto?> GetAsync(Guid id, CancellationToken ct = default);
        Task<FacilityDto> AddAsync(FacilityDto dto, CancellationToken ct = default);
        Task<FacilityDto> UpdateAsync(FacilityDto dto, CancellationToken ct = default);
        Task DeleteAsync(Guid id, CancellationToken ct = default);
        Task<IReadOnlyList<FacilityPatientAssignmentDto>> GetPatientAssignmentsAsync(Guid facilityId, bool includeDischarged = true, CancellationToken ct = default);
        Task<FacilityPatientAssignmentDto> AdmitPatientAsync(Guid facilityId, AdmitFacilityPatientDto dto, CancellationToken ct = default);
        Task<FacilityPatientAssignmentDto> DischargePatientAsync(Guid facilityId, Guid assignmentId, DischargeFacilityPatientDto? dto, CancellationToken ct = default);
    }
}
