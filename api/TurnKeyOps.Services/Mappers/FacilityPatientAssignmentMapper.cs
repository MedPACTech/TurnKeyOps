using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class FacilityPatientAssignmentMapper
    {
        public static FacilityPatientAssignmentDto ToDto(FacilityPatientAssignment entity)
        {
            return new FacilityPatientAssignmentDto
            {
                Id = entity.Id,
                FacilityId = entity.FacilityId,
                PatientId = entity.PatientId,
                PatientFirstName = entity.PatientFirstName,
                PatientLastName = entity.PatientLastName,
                AdmitDate = DateTime.SpecifyKind(entity.AdmitDate, DateTimeKind.Utc),
                DischargeDate = entity.DischargeDate.HasValue
                    ? DateTime.SpecifyKind(entity.DischargeDate.Value, DateTimeKind.Utc)
                    : null,
                Status = entity.Status,
                DateCreated = DateTime.SpecifyKind(entity.DateCreated, DateTimeKind.Utc),
                DateUpdated = entity.DateUpdated.HasValue
                    ? DateTime.SpecifyKind(entity.DateUpdated.Value, DateTimeKind.Utc)
                    : null
            };
        }
    }
}
