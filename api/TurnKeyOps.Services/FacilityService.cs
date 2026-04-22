using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class FacilityService : IFacilityService
    {
        private readonly IFacilityRepository _facilityRepository;
        private readonly IFacilityPatientAssignmentRepository _facilityPatientAssignmentRepository;
        private readonly IPatientRepository _patientRepository;
        private readonly IUserContext _userContext;

        public FacilityService(
            IFacilityRepository facilityRepository,
            IFacilityPatientAssignmentRepository facilityPatientAssignmentRepository,
            IPatientRepository patientRepository,
            IUserContext userContext)
        {
            _facilityRepository = facilityRepository;
            _facilityPatientAssignmentRepository = facilityPatientAssignmentRepository;
            _patientRepository = patientRepository;
            _userContext = userContext;
        }

        public async Task<IReadOnlyList<FacilityDto>> GetAllAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var facilities = await _facilityRepository.GetByPartitionAsync(PartitionKeyForTenant(), ct);
            return facilities
                .OrderBy(x => x.FacilityName, StringComparer.OrdinalIgnoreCase)
                .Select(FacilityMapper.ToDto)
                .ToList();
        }

        public async Task<FacilityDto?> GetAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var entity = await _facilityRepository.GetAsync(PartitionKeyForTenant(), EntityKeyPolicy.Row(id), ct);
            return entity is null ? null : FacilityMapper.ToDto(entity);
        }

        public async Task<FacilityDto> AddAsync(FacilityDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            Validate(dto);

            var now = DateTime.UtcNow;
            if (dto.Id == Guid.Empty)
                dto.Id = Guid.NewGuid();

            dto.DateCreated = now;
            dto.DateUpdated = now;

            var entity = FacilityMapper.ToEntity(dto, PartitionKeyForTenant());
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            entity.DateCreated = now;
            entity.DateUpdated = now;

            var saved = await _facilityRepository.SaveAsync(entity, ct);
            return FacilityMapper.ToDto(saved);
        }

        public async Task<FacilityDto> UpdateAsync(FacilityDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            Validate(dto);

            if (dto.Id == Guid.Empty)
                throw new ArgumentException("Facility id is required.", nameof(dto));

            var existing = await _facilityRepository.GetAsync(PartitionKeyForTenant(), EntityKeyPolicy.Row(dto.Id), ct)
                ?? throw new KeyNotFoundException("Facility not found.");

            existing.FacilityName = dto.FacilityName.Trim();
            existing.LogoUrl = Normalize(dto.LogoUrl);
            existing.Website = Normalize(dto.Website);
            existing.AddressLine1 = Normalize(dto.AddressLine1);
            existing.AddressLine2 = Normalize(dto.AddressLine2);
            existing.City = Normalize(dto.City);
            existing.State = Normalize(dto.State);
            existing.PostalCode = Normalize(dto.PostalCode);
            existing.IsResidential = dto.IsResidential;
            existing.NumberOfBeds = dto.NumberOfBeds;
            existing.PointOfContactName = Normalize(dto.PointOfContactName);
            existing.PointOfContactEmail = Normalize(dto.PointOfContactEmail);
            existing.PointOfContactPhone = Normalize(dto.PointOfContactPhone);
            existing.DateUpdated = DateTime.UtcNow;

            var saved = await _facilityRepository.SaveAsync(existing, ct);
            return FacilityMapper.ToDto(saved);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var existing = await _facilityRepository.GetAsync(PartitionKeyForTenant(), EntityKeyPolicy.Row(id), ct)
                ?? throw new KeyNotFoundException("Facility not found.");

            existing.IsDeleted = true;
            existing.DateUpdated = DateTime.UtcNow;

            await _facilityRepository.SaveAsync(existing, ct);
        }

        public async Task<IReadOnlyList<FacilityPatientAssignmentDto>> GetPatientAssignmentsAsync(Guid facilityId, bool includeDischarged = true, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await RequireFacilityAsync(facilityId, ct);

            var assignments = await _facilityPatientAssignmentRepository.GetByFacilityAsync(FacilityAssignmentPartitionKey(facilityId), ct);
            return assignments
                .Where(x => includeDischarged || string.Equals(x.Status, "Admitted", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.AdmitDate)
                .ThenBy(x => x.PatientLastName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.PatientFirstName, StringComparer.OrdinalIgnoreCase)
                .Select(FacilityPatientAssignmentMapper.ToDto)
                .ToList();
        }

        public async Task<FacilityPatientAssignmentDto> AdmitPatientAsync(Guid facilityId, AdmitFacilityPatientDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            if (dto.PatientId == Guid.Empty)
                throw new ArgumentException("Patient id is required.", nameof(dto));

            var facility = await RequireFacilityAsync(facilityId, ct);
            var patient = await _patientRepository.GetAsync(PartitionKeyForTenant(), EntityKeyPolicy.Row(dto.PatientId), ct)
                ?? throw new KeyNotFoundException("Patient not found.");

            if (patient.CurrentFacilityId.HasValue)
                throw new InvalidOperationException("Patient is already assigned to a facility.");

            var admitDate = dto.AdmitDate.HasValue
                ? DateTime.SpecifyKind(dto.AdmitDate.Value, DateTimeKind.Utc)
                : DateTime.UtcNow;
            var now = DateTime.UtcNow;
            var assignmentId = Guid.NewGuid();
            var assignment = new FacilityPatientAssignment
            {
                Id = assignmentId,
                FacilityId = facilityId,
                PatientId = patient.Id,
                PatientFirstName = patient.FirstName,
                PatientLastName = patient.LastName,
                AdmitDate = admitDate,
                Status = "Admitted",
                DateCreated = now,
                DateUpdated = now,
                PartitionKey = FacilityAssignmentPartitionKey(facilityId),
                RowKey = FacilityAssignmentRowKey(admitDate, patient.Id, assignmentId)
            };

            await _facilityPatientAssignmentRepository.SaveAsync(assignment, ct);

            if (facility.IsResidential)
            {
                patient.PreFacilityPhysicalAddressLine1 = patient.PhysicalAddressLine1;
                patient.PreFacilityPhysicalAddressLine2 = patient.PhysicalAddressLine2;
                patient.PreFacilityPhysicalCity = patient.PhysicalCity;
                patient.PreFacilityPhysicalState = patient.PhysicalState;
                patient.PreFacilityPhysicalPostalCode = patient.PhysicalPostalCode;
                patient.PreFacilityPhysicalCountry = patient.PhysicalCountry;
                patient.PhysicalAddressLine1 = Normalize(facility.AddressLine1);
                patient.PhysicalAddressLine2 = Normalize(facility.AddressLine2);
                patient.PhysicalCity = Normalize(facility.City);
                patient.PhysicalState = Normalize(facility.State);
                patient.PhysicalPostalCode = Normalize(facility.PostalCode);
                patient.PhysicalCountry = null;
            }
            patient.CurrentFacilityId = facility.Id;
            patient.CurrentFacilityName = facility.FacilityName;
            patient.CurrentFacilityAdmitDate = admitDate;
            patient.CurrentFacilityStatus = "Admitted";
            patient.DateUpdated = now;
            await _patientRepository.SaveAsync(patient, ct);

            return FacilityPatientAssignmentMapper.ToDto(assignment);
        }

        public async Task<FacilityPatientAssignmentDto> DischargePatientAsync(Guid facilityId, Guid assignmentId, DischargeFacilityPatientDto? dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var facility = await RequireFacilityAsync(facilityId, ct);
            var assignments = await _facilityPatientAssignmentRepository.GetByFacilityAsync(FacilityAssignmentPartitionKey(facilityId), ct);
            var assignment = assignments.FirstOrDefault(x => x.Id == assignmentId)
                ?? throw new KeyNotFoundException("Facility patient assignment not found.");

            if (!string.Equals(assignment.Status, "Admitted", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Only admitted assignments can be discharged.");

            var dischargeDate = dto?.DischargeDate.HasValue == true
                ? DateTime.SpecifyKind(dto.DischargeDate.Value, DateTimeKind.Utc)
                : DateTime.UtcNow;
            var now = DateTime.UtcNow;

            assignment.DischargeDate = dischargeDate;
            assignment.Status = "Discharged";
            assignment.DateUpdated = now;
            await _facilityPatientAssignmentRepository.SaveAsync(assignment, ct);

            var patient = await _patientRepository.GetAsync(PartitionKeyForTenant(), EntityKeyPolicy.Row(assignment.PatientId), ct)
                ?? throw new KeyNotFoundException("Patient not found.");

            if (patient.CurrentFacilityId == facility.Id)
            {
                if (facility.IsResidential)
                {
                    patient.PhysicalAddressLine1 = patient.PreFacilityPhysicalAddressLine1;
                    patient.PhysicalAddressLine2 = patient.PreFacilityPhysicalAddressLine2;
                    patient.PhysicalCity = patient.PreFacilityPhysicalCity;
                    patient.PhysicalState = patient.PreFacilityPhysicalState;
                    patient.PhysicalPostalCode = patient.PreFacilityPhysicalPostalCode;
                    patient.PhysicalCountry = patient.PreFacilityPhysicalCountry;
                    patient.PreFacilityPhysicalAddressLine1 = null;
                    patient.PreFacilityPhysicalAddressLine2 = null;
                    patient.PreFacilityPhysicalCity = null;
                    patient.PreFacilityPhysicalState = null;
                    patient.PreFacilityPhysicalPostalCode = null;
                    patient.PreFacilityPhysicalCountry = null;
                }
                patient.CurrentFacilityId = null;
                patient.CurrentFacilityName = null;
                patient.CurrentFacilityAdmitDate = null;
                patient.CurrentFacilityStatus = null;
                patient.DateUpdated = now;
                await _patientRepository.SaveAsync(patient, ct);
            }

            return FacilityPatientAssignmentMapper.ToDto(assignment);
        }

        private string PartitionKeyForTenant() => EntityKeyPolicy.TenantPartition(_userContext.TenantId);
        private string FacilityAssignmentPartitionKey(Guid facilityId) => $"{PartitionKeyForTenant()}|FACILITY={facilityId:D}";
        private static string FacilityAssignmentRowKey(DateTime admitDateUtc, Guid patientId, Guid assignmentId)
            => $"ADMIT={admitDateUtc:yyyyMMddHHmmss}|PATIENT={patientId:D}|{assignmentId:D}";

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private async Task<FacilityDto> RequireFacilityAsync(Guid facilityId, CancellationToken ct)
        {
            var facility = await GetAsync(facilityId, ct)
                ?? throw new KeyNotFoundException("Facility not found.");

            return facility;
        }

        private static void Validate(FacilityDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.FacilityName))
                throw new ArgumentException("Facility name is required.", nameof(dto));

            if (dto.NumberOfBeds.HasValue && dto.NumberOfBeds.Value < 0)
                throw new ArgumentException("NumberOfBeds cannot be negative.", nameof(dto));
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
