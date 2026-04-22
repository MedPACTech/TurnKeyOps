using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using System.Globalization;

namespace MedInsights.Services
{
    public class PatientDiagnosisService : IPatientDiagnosisService
    {
        private const int ActiveStatusId = 1;
        private static readonly IReadOnlyDictionary<int, string> DiagnosisStatusMap = new Dictionary<int, string>
        {
            { 1, "Active" },
            { 2, "In Remission" },
            { 3, "Resolved" },
            { 4, "Inactive" },
            { 5, "Not Applicable" }
        };

        private readonly IPatientDiagnosisRepository _repository;
        private readonly IDiagnosisCodeService _diagnosisCodeService;
        private readonly IUserContext _userContext;

        public PatientDiagnosisService(
            IPatientDiagnosisRepository repository,
            IDiagnosisCodeService diagnosisCodeService,
            IUserContext userContext)
        {
            _repository = repository;
            _diagnosisCodeService = diagnosisCodeService;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        public async Task<PatientDiagnosisDto?> GetAsync(Guid patientId, Guid diagnosisId)
        {
            EnsureAuthenticated();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(diagnosisId);
            var item = await _repository.GetAsync(pk, rowKey);

            return item == null ? null : PatientDiagnosisMapper.ToDto(item);
        }

        public async Task<IEnumerable<PatientDiagnosisDto>> GetByPatientAsync(Guid patientId)
        {
            EnsureAuthenticated();

            var pk = PartitionKeyForPatient(patientId);
            var items = await _repository.GetByPatientAsync(pk);

            return items
                .Select(PatientDiagnosisMapper.ToDto)
                .OrderByDescending(x => x.DateDiagnosed ?? DateOnly.MinValue);
        }

        public async Task<PatientDiagnosisDto> AddAsync(PatientDiagnosisDto dto)
        {
            EnsureAuthenticated();
            EnsurePatientIdProvided(dto.PatientId);
            EnsureCodeIdProvided(dto.DiagnosisCodeId);

            var entity = PatientDiagnosisMapper.ToEntity(dto);
            ApplyStatusDefaultsAndValidation(entity);
            await EnsureNoDuplicateActiveDiagnosisAsync(entity, excludeDiagnosisId: null);

            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForPatient(dto.PatientId);
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            entity.DateDiagnosed = NormalizeStoredDate(entity.DateDiagnosed);
            if (string.IsNullOrWhiteSpace(entity.DateDiagnosed))
                entity.DateDiagnosed = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            entity.IsDeleted = false;

            await PopulateCodeDetailsAsync(entity);

            var saved = await _repository.SaveAsync(entity);
            return PatientDiagnosisMapper.ToDto(saved);
        }

        public async Task<PatientDiagnosisDto> UpdateAsync(PatientDiagnosisDto dto)
        {
            EnsureAuthenticated();
            EnsurePatientIdProvided(dto.PatientId);
            EnsureCodeIdProvided(dto.DiagnosisCodeId);

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Diagnosis record not found.");

            var entity = PatientDiagnosisMapper.ToEntity(dto);
            entity.Id = existing.Id == Guid.Empty ? dto.Id : existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;
            entity.DateDiagnosed = NormalizeStoredDate(entity.DateDiagnosed);
            if (string.IsNullOrWhiteSpace(entity.DateDiagnosed))
                entity.DateDiagnosed = NormalizeStoredDate(existing.DateDiagnosed);
            entity.IsDeleted = existing.IsDeleted;
            ApplyStatusDefaultsAndValidation(entity);
            await EnsureNoDuplicateActiveDiagnosisAsync(entity, excludeDiagnosisId: entity.Id);

            await PopulateCodeDetailsAsync(entity);

            var saved = await _repository.SaveAsync(entity);
            return PatientDiagnosisMapper.ToDto(saved);
        }

        public async Task DeleteAsync(PatientDiagnosisDto dto)
        {
            EnsureAuthenticated();
            EnsurePatientIdProvided(dto.PatientId);

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Diagnosis record not found.");

            existing.IsDeleted = true;
            await _repository.SaveAsync(existing);
        }

        private async Task PopulateCodeDetailsAsync(PatientDiagnosis entity)
        {
            var code = await _diagnosisCodeService.GetByIdAsync(entity.DiagnosisCodeId)
                ?? throw new KeyNotFoundException("Diagnosis code not found.");

            entity.DiagnosisCode = code.Code;
            entity.ShortDescription = code.ShortDescription;
            entity.LongDescription = code.LongDescription;
        }

        private async Task EnsureNoDuplicateActiveDiagnosisAsync(PatientDiagnosis entity, Guid? excludeDiagnosisId)
        {
            if (entity.DiagnosisStatusId != ActiveStatusId)
                return;

            var existingDiagnoses = await _repository.GetByPatientAsync(PartitionKeyForPatient(entity.PatientId));
            var hasDuplicateActiveDiagnosis = existingDiagnoses.Any(x =>
                x.DiagnosisCodeId == entity.DiagnosisCodeId &&
                x.DiagnosisStatusId == ActiveStatusId &&
                (!excludeDiagnosisId.HasValue || x.Id != excludeDiagnosisId.Value));

            if (hasDuplicateActiveDiagnosis)
            {
                throw new InvalidOperationException("Patient already has an active diagnosis with this diagnosis code.");
            }
        }

        private static void ApplyStatusDefaultsAndValidation(PatientDiagnosis entity)
        {
            if (entity.DiagnosisStatusId <= 0)
                entity.DiagnosisStatusId = ActiveStatusId;

            if (!DiagnosisStatusMap.TryGetValue(entity.DiagnosisStatusId, out var statusText))
                throw new ArgumentOutOfRangeException(nameof(entity.DiagnosisStatusId), $"Invalid diagnosis status id: {entity.DiagnosisStatusId}");

            entity.DiagnosisStatus = statusText;
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();
        }

        private static void EnsureCodeIdProvided(Guid diagnosisCodeId)
        {
            if (diagnosisCodeId == Guid.Empty)
                throw new ArgumentException("DiagnosisCodeId is required.", nameof(diagnosisCodeId));
        }

        private static void EnsurePatientIdProvided(Guid patientId)
        {
            if (patientId == Guid.Empty)
                throw new ArgumentException("PatientId is required.", nameof(patientId));
        }

        private static string NormalizeStoredDate(string? rawDate)
        {
            if (string.IsNullOrWhiteSpace(rawDate))
                return string.Empty;

            var normalized = rawDate.Trim().Trim('"');
            if (DateOnly.TryParseExact(
                    normalized,
                    new[] { "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsedDate))
            {
                return parsedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            }

            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDateTime))
                return DateOnly.FromDateTime(parsedDateTime).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            return string.Empty;
        }
    }
}
