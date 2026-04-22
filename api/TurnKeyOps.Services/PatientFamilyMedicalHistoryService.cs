using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientFamilyMedicalHistoryService : IPatientFamilyMedicalHistoryService
    {
        private readonly IPatientFamilyMedicalHistoryRepository _repository;
        private readonly IUserContext _userContext;

        public PatientFamilyMedicalHistoryService(IPatientFamilyMedicalHistoryRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        public async Task<PatientFamilyMedicalHistoryDto?> GetAsync(Guid patientId, Guid familyMedicalHistoryId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(familyMedicalHistoryId);
            var item = await _repository.GetAsync(pk, rowKey);

            return item == null ? null : PatientFamilyMedicalHistoryMapper.ToDto(item);
        }

        public async Task<IEnumerable<PatientFamilyMedicalHistoryDto>> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var items = await _repository.GetByPatientAsync(pk);

            return items
                .OrderByDescending(x => x.DateNoted)
                .Select(PatientFamilyMedicalHistoryMapper.ToDto);
        }

        public async Task<PatientFamilyMedicalHistoryDto> AddAsync(PatientFamilyMedicalHistoryDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var entity = PatientFamilyMedicalHistoryMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForPatient(dto.PatientId);
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            if (entity.DateNoted == default) entity.DateNoted = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(entity);
            return PatientFamilyMedicalHistoryMapper.ToDto(saved);
        }

        public async Task<PatientFamilyMedicalHistoryDto> UpdateAsync(PatientFamilyMedicalHistoryDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Family medical history record not found.");

            var entity = PatientFamilyMedicalHistoryMapper.ToEntity(dto);
            entity.Id = existing.Id == Guid.Empty ? dto.Id : existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;
            if (entity.DateNoted == default) entity.DateNoted = existing.DateNoted;

            var saved = await _repository.SaveAsync(entity);
            return PatientFamilyMedicalHistoryMapper.ToDto(saved);
        }

        public async Task DeleteAsync(PatientFamilyMedicalHistoryDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Family medical history record not found.");
            existing.IsDeleted = true;
            await _repository.SaveAsync(existing);
        }
    }
}
