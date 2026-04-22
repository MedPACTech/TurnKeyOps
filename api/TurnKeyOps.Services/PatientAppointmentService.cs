using MedInsights.Lib.Dtos;
using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Enums;
using MedInsights.Lib.Models;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using Microsoft.Extensions.Options;

namespace MedInsights.Services
{
    public class PatientAppointmentService : IPatientAppointmentService
    {
        private readonly IPatientAppointmentRepository _repository;
        private readonly IAppointmentTypeRepository _appointmentTypeRepository;
        private readonly IUserContext _userContext;
        private readonly IPatientService _patientService;
        private readonly IPatientContactService _patientContactService;
        private readonly AppointmentDataCompletenessSettings _completenessSettings;

        public PatientAppointmentService(
            IPatientAppointmentRepository repository,
            IAppointmentTypeRepository appointmentTypeRepository,
            IUserContext userContext,
            IPatientService patientService,
            IPatientContactService patientContactService,
            IOptions<AppointmentDataCompletenessSettings> completenessSettings)
        {
            _repository = repository;
            _appointmentTypeRepository = appointmentTypeRepository;
            _userContext = userContext;
            _patientService = patientService;
            _patientContactService = patientContactService;
            _completenessSettings = completenessSettings?.Value ?? new AppointmentDataCompletenessSettings();
        }

        private string PartitionKeyForTenant() => EntityKeyPolicy.TenantPartition(_userContext.TenantId);

        public async Task<PatientAppointmentDto?> GetAsync(Guid id)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForTenant();
            var rowKey = EntityKeyPolicy.Row(id);
            var entity = await _repository.GetAsync(pk, rowKey);

            return entity == null ? null : PatientAppointmentMapper.ToDto(entity);
        }

        public async Task<PatientAppointmentDto> AddAsync(PatientAppointmentDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
            ValidateAppointmentWindow(dto);
            var appointmentType = await ResolveAppointmentTypeAsync(dto.AppointmentTypeId, dto.AppointmentType, allowInactive: false);
            dto.AppointmentTypeId = appointmentType.Id;
            dto.AppointmentType = appointmentType.Name;
            var warnings = await ApplyCompletenessRulesAsync(dto);

            var entity = PatientAppointmentMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForTenant();
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            entity.CreatedBy = _userContext.UserId.ToString();
            entity.DateCreated = DateTime.UtcNow;
            entity.DateUpdated = null;

            var saved = await _repository.SaveAsync(entity);
            var result = PatientAppointmentMapper.ToDto(saved);
            result.ValidationWarnings = warnings;
            return result;
        }

        public async Task<PatientAppointmentDto> UpdateAsync(PatientAppointmentDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
            var pk = PartitionKeyForTenant();
            var existing = await _repository.GetAsync(pk, EntityKeyPolicy.Row(dto.Id))
                ?? throw new KeyNotFoundException("Appointment not found.");

            ValidateAppointmentWindow(dto);
            var allowInactive = existing.AppointmentTypeId == dto.AppointmentTypeId && dto.AppointmentTypeId != Guid.Empty;
            var appointmentType = await ResolveAppointmentTypeAsync(dto.AppointmentTypeId, dto.AppointmentType, allowInactive);
            dto.AppointmentTypeId = appointmentType.Id;
            dto.AppointmentType = appointmentType.Name;
            var warnings = await ApplyCompletenessRulesAsync(dto);

            existing.AppointmentTypeId = dto.AppointmentTypeId;
            existing.AppointmentTypeName = dto.AppointmentType;
            existing.AppointmentType = AppointmentType.Other;
            existing.AppointmentStartTime = dto.AppointmentStartTime;
            existing.AppointmentEndTime = dto.AppointmentEndTime;
            existing.Reason = dto.Reason;
            existing.AppointmentStatus = dto.AppointmentStatus;
            existing.AppointmentLocation = dto.AppointmentLocation;
            existing.PatientFirstName = dto.PatientFirstName;
            existing.PatientLastName = dto.PatientLastName;
            existing.PrimaryContactId = dto.PrimaryContactId.HasValue ? EntityKeyPolicy.Row(dto.PrimaryContactId.Value) : null;
            existing.PrimaryContactFirstName = Normalize(dto.PrimaryContactFirstName) ?? string.Empty;
            existing.PrimaryContactLastName = Normalize(dto.PrimaryContactLastName) ?? string.Empty;
            existing.PrimaryContactRelationship = Normalize(dto.PrimaryContactRelationship) ?? string.Empty;
            existing.PrimaryContactPhone = Normalize(dto.PrimaryContactPhone) ?? string.Empty;
            existing.PrimaryContactEmail = Normalize(dto.PrimaryContactEmail) ?? string.Empty;
            existing.VisitAddressLine1 = Normalize(dto.VisitAddressLine1) ?? string.Empty;
            existing.VisitAddressLine2 = Normalize(dto.VisitAddressLine2) ?? string.Empty;
            existing.VisitCity = Normalize(dto.VisitCity) ?? string.Empty;
            existing.VisitState = Normalize(dto.VisitState) ?? string.Empty;
            existing.VisitPostalCode = Normalize(dto.VisitPostalCode) ?? string.Empty;
            existing.VisitCountry = Normalize(dto.VisitCountry) ?? string.Empty;
            existing.UserId = EntityKeyPolicy.Row(dto.UserId);
            existing.DisplayName = dto.UserName;
            existing.DateUpdated = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(existing);
            var result = PatientAppointmentMapper.ToDto(saved);
            result.ValidationWarnings = warnings;
            return result;
        }

        public async Task DeleteAsync(Guid id)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
            var existing = await _repository.GetAsync(PartitionKeyForTenant(), EntityKeyPolicy.Row(id))
                ?? throw new KeyNotFoundException("Appointment not found.");
            existing.IsDeleted = true;
            existing.DateUpdated = DateTime.UtcNow;
            await _repository.SaveAsync(existing);
        }

        public sealed class AppointmentSearch
        {
            public Guid? PatientId { get; set; }
            public Guid? ProviderId { get; set; }
            public DateTime? On { get; set; }
            public DateTime? Start { get; set; }
            public DateTime? End { get; set; }
            public int Page { get; set; } = 1;
            public int PageSize { get; set; } = 50;
            public string? Sort { get; set; }
            public string? Order { get; set; }
        }

        public async Task<IEnumerable<PatientAppointmentDto>> SearchAsync(
            AppointmentSearch filter,
            CancellationToken cancellationToken = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            if (filter is null) throw new ArgumentNullException(nameof(filter));
            if (filter.Page <= 0) filter.Page = 1;
            if (filter.PageSize <= 0 || filter.PageSize > 500) filter.PageSize = 50;

            DateTime? fromUtc = null;
            DateTime? toExclusiveUtc = null;

            if (filter.On.HasValue)
            {
                fromUtc = DateTimeHelper.DateFloorUtc(filter.On.Value, _userContext.Timezone);
                toExclusiveUtc = DateTimeHelper.DateCeilingUtcExclusive(filter.On.Value, _userContext.Timezone);
            }
            else if (filter.Start.HasValue || filter.End.HasValue)
            {
                if (!filter.Start.HasValue || !filter.End.HasValue)
                    throw new ArgumentException("Both Start and End must be provided when filtering by range.");

                fromUtc = DateTimeHelper.DateFloorUtc(filter.Start.Value, _userContext.Timezone);
                toExclusiveUtc = DateTimeHelper.DateCeilingUtcExclusive(filter.End.Value, _userContext.Timezone);

                if (fromUtc >= toExclusiveUtc)
                    throw new ArgumentException("Start must be earlier than End.");
            }

            var repoFilter = new AppointmentSearchRepositoryFilter
            {
                TenantPartitionKey = PartitionKeyForTenant(),
                PatientRowKey = filter.PatientId.HasValue ? EntityKeyPolicy.Row(filter.PatientId.Value) : null,
                ProviderRowKey = filter.ProviderId.HasValue ? EntityKeyPolicy.Row(filter.ProviderId.Value) : null,
                FromUtc = fromUtc,
                ToExclusiveUtc = toExclusiveUtc,
                Page = filter.Page,
                PageSize = filter.PageSize,
                Sort = filter.Sort,
                Order = filter.Order
            };

            var entities = await _repository.SearchAsync(repoFilter, cancellationToken);
            return entities.Select(PatientAppointmentMapper.ToDto).ToList();
        }

        public async Task<IEnumerable<PatientAppointmentDto>> SearchAsync(
            AppointmentSearchRepositoryFilter repoFilter,
            CancellationToken cancellationToken = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            if (repoFilter is null) throw new ArgumentNullException(nameof(repoFilter));
            if (string.IsNullOrWhiteSpace(repoFilter.TenantPartitionKey))
                repoFilter.TenantPartitionKey = PartitionKeyForTenant();

            var entities = await _repository.SearchAsync(repoFilter, cancellationToken);
            return entities.Select(PatientAppointmentMapper.ToDto).ToList();
        }

        private static void ValidateAppointmentWindow(PatientAppointmentDto dto)
        {
            if (dto.AppointmentStartTime <= DateTime.UtcNow)
                throw new ArgumentException("Appointment start time must be in the future.");

            if (dto.AppointmentEndTime <= dto.AppointmentStartTime)
                throw new ArgumentException("Appointment end time must be after start time.");
        }

        private async Task<MedInsights.Lib.Entities.AppointmentTypeDefinition> ResolveAppointmentTypeAsync(
            Guid appointmentTypeId,
            string? appointmentTypeName,
            bool allowInactive,
            CancellationToken ct = default)
        {
            if (appointmentTypeId == Guid.Empty)
            {
                if (string.IsNullOrWhiteSpace(appointmentTypeName))
                    throw new ArgumentException("AppointmentTypeId is required.");

                var byName = (await _appointmentTypeRepository.GetByTenantAsync(_userContext.TenantId, ct))
                    .FirstOrDefault(x => string.Equals(x.Name, appointmentTypeName.Trim(), StringComparison.OrdinalIgnoreCase));

                if (byName is null)
                    throw new ArgumentException("AppointmentType is invalid.");

                if (!allowInactive && !byName.IsActive)
                    throw new InvalidOperationException("Appointment type is inactive.");

                return byName;
            }

            var definition = await _appointmentTypeRepository.GetByIdAsync(
                _userContext.TenantId,
                appointmentTypeId,
                ct,
                includeDeleted: allowInactive);

            if (definition is null)
                throw new ArgumentException("AppointmentTypeId is invalid.");

            if (!allowInactive && !definition.IsActive)
                throw new InvalidOperationException("Appointment type is inactive.");

            return definition;
        }

        private async Task<List<string>> ApplyCompletenessRulesAsync(PatientAppointmentDto dto)
        {
            var warnings = new List<string>();
            var strict = _completenessSettings.ValidationMode == AppointmentValidationMode.Strict;

            dto.PatientFirstName = Normalize(dto.PatientFirstName) ?? string.Empty;
            dto.PatientLastName = Normalize(dto.PatientLastName) ?? string.Empty;
            dto.Reason = Normalize(dto.Reason) ?? string.Empty;
            dto.VisitAddressLine1 = Normalize(dto.VisitAddressLine1) ?? string.Empty;
            dto.VisitAddressLine2 = Normalize(dto.VisitAddressLine2) ?? string.Empty;
            dto.VisitCity = Normalize(dto.VisitCity) ?? string.Empty;
            dto.VisitState = Normalize(dto.VisitState) ?? string.Empty;
            dto.VisitPostalCode = Normalize(dto.VisitPostalCode) ?? string.Empty;
            dto.VisitCountry = Normalize(dto.VisitCountry) ?? string.Empty;

            var patient = await _patientService.GetAsync(dto.PatientId)
                ?? throw new KeyNotFoundException("Patient not found.");
            var contacts = (await _patientContactService.GetByPatientAsync(dto.PatientId)).ToList();

            if (!contacts.Any(c => c.Relationship == PatientRelationship.Self))
                throw new InvalidOperationException("Patient must have a Self contact.");

            var primaryContacts = contacts.Where(c => c.IsPrimary).ToList();
            if (primaryContacts.Count > 1)
                throw new InvalidOperationException("Patient has multiple primary contacts.");

            PatientContactDto? selectedContact = null;
            if (dto.PrimaryContactId.HasValue)
            {
                selectedContact = contacts.FirstOrDefault(c => c.Id == dto.PrimaryContactId.Value)
                    ?? throw new ArgumentException("PrimaryContactId does not belong to the patient.");

                if (!selectedContact.IsPrimary)
                    throw new ArgumentException("PrimaryContactId must reference the patient's primary contact.");
            }
            else
            {
                selectedContact = primaryContacts.FirstOrDefault();
            }

            if (selectedContact == null)
            {
                EnforceOrWarn(
                    strict,
                    warnings,
                    "Primary contact is required for appointment.");
            }
            else
            {
                dto.PrimaryContactId = selectedContact.Id;
                dto.PrimaryContactFirstName = selectedContact.FirstName;
                dto.PrimaryContactLastName = selectedContact.LastName;
                dto.PrimaryContactRelationship = selectedContact.Relationship == PatientRelationship.Other
                    ? (Normalize(selectedContact.OtherRelationship) ?? PatientRelationship.Other.ToString())
                    : selectedContact.Relationship.ToString();
                dto.PrimaryContactPhone = Normalize(selectedContact.PrimaryPhone) ?? string.Empty;
                dto.PrimaryContactEmail = Normalize(selectedContact.Email) ?? string.Empty;

                if (IsMinorAt(patient, dto.AppointmentStartTime) && selectedContact.Relationship == PatientRelationship.Self)
                    warnings.Add("Minor patient has Self as primary contact.");
            }

            if (dto.AppointmentLocation == AppointmentLocation.Patient_Home)
            {
                var missingAddress =
                    string.IsNullOrWhiteSpace(dto.VisitAddressLine1) ||
                    string.IsNullOrWhiteSpace(dto.VisitCity) ||
                    string.IsNullOrWhiteSpace(dto.VisitState) ||
                    string.IsNullOrWhiteSpace(dto.VisitPostalCode);

                if (missingAddress)
                {
                    EnforceOrWarn(
                        strict,
                        warnings,
                        "Patient home appointments require visit address line 1, city, state, and postal code.");
                }
            }

            dto.ValidationWarnings = warnings.ToList();
            return warnings;
        }

        private static void EnforceOrWarn(bool strict, List<string> warnings, string message)
        {
            if (strict)
                throw new ArgumentException(message);

            warnings.Add(message);
        }

        private static bool IsMinorAt(PatientDto patient, DateTime atUtc)
        {
            var date = atUtc.Date;
            var dob = patient.DateOfBirth;
            var age = date.Year - dob.Year;
            if (date < dob.ToDateTime(TimeOnly.MinValue).Date.AddYears(age))
                age--;

            return age < 18;
        }

        private static string? Normalize(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
