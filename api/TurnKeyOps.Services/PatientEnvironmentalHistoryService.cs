using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientEnvironmentalHistoryService : IPatientEnvironmentalHistoryService
    {
        private readonly IPatientEnvironmentalHistoryRepository _repository;
        private readonly IUserContext _userContext;

        public PatientEnvironmentalHistoryService(IPatientEnvironmentalHistoryRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        private async Task<PatientEnvironmentalHistory?> GetSingleActiveAsync(string partitionKey)
        {
            var items = (await _repository.GetByPatientAsync(partitionKey)).ToList();
            if (items.Count > 1)
                throw new InvalidOperationException("Multiple active environmental history records found for patient.");

            return items.FirstOrDefault();
        }

        public async Task<PatientEnvironmentalHistoryDto?> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var single = await GetSingleActiveAsync(pk);

            return single == null ? null : PatientEnvironmentalHistoryMapper.ToDto(single);
        }

        public async Task<PatientEnvironmentalHistoryDto> AddAsync(PatientEnvironmentalHistoryDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var existingSingle = await GetSingleActiveAsync(pk);
            if (existingSingle != null)
                throw new InvalidOperationException("Environmental history already exists for this patient. Use update.");

            var entity = PatientEnvironmentalHistoryMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = pk;
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);

            if (entity.DateNoted == default) entity.DateNoted = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(entity);
            return PatientEnvironmentalHistoryMapper.ToDto(saved);
        }

        public async Task<PatientEnvironmentalHistoryDto> UpdateAsync(PatientEnvironmentalHistoryDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var existing = await GetSingleActiveAsync(pk)
                ?? throw new KeyNotFoundException("Environmental history record not found.");

            var entity = PatientEnvironmentalHistoryMapper.ToEntity(dto);
            entity.Id = existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;
            if (entity.DateNoted == default) entity.DateNoted = existing.DateNoted;

            var saved = await _repository.SaveAsync(entity);
            return PatientEnvironmentalHistoryMapper.ToDto(saved);
        }

        public async Task DeleteAsync(PatientEnvironmentalHistoryDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            if (dto.Id != Guid.Empty)
            {
                var existingById = await _repository.GetAsync(pk, EntityKeyPolicy.Row(dto.Id))
                    ?? throw new KeyNotFoundException("Environmental history record not found.");
                existingById.IsDeleted = true;
                await _repository.SaveAsync(existingById);
                return;
            }

            var existing = await GetSingleActiveAsync(pk);

            if (existing == null) return;
            existing.IsDeleted = true;
            await _repository.SaveAsync(existing);
        }
    }
}
