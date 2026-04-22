using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientMilitaryFirstResponderService : IPatientMilitaryFirstResponderService
    {
        private readonly IPatientMilitaryFirstResponderRepository _repository;
        private readonly IUserContext _userContext;

        public PatientMilitaryFirstResponderService(IPatientMilitaryFirstResponderRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        private async Task<PatientMilitaryFirstResponder?> GetSingleActiveAsync(string partitionKey)
        {
            var items = (await _repository.GetByPatientAsync(partitionKey)).ToList();
            if (items.Count > 1)
                throw new InvalidOperationException("Multiple active military/first responder records found for patient.");

            return items.FirstOrDefault();
        }

        public async Task<PatientMilitaryFirstResponderDto?> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var single = await GetSingleActiveAsync(pk);

            return single == null ? null : PatientMilitaryFirstResponderMapper.ToDto(single);
        }

        public async Task<PatientMilitaryFirstResponderDto> AddAsync(PatientMilitaryFirstResponderDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var existingSingle = await GetSingleActiveAsync(pk);
            if (existingSingle != null)
                throw new InvalidOperationException("Military/first responder record already exists for this patient. Use update.");

            var entity = PatientMilitaryFirstResponderMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = pk;
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);

            var saved = await _repository.SaveAsync(entity);
            return PatientMilitaryFirstResponderMapper.ToDto(saved);
        }

        public async Task<PatientMilitaryFirstResponderDto> UpdateAsync(PatientMilitaryFirstResponderDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var existing = await GetSingleActiveAsync(pk)
                ?? throw new KeyNotFoundException("Military/first responder record not found.");

            var entity = PatientMilitaryFirstResponderMapper.ToEntity(dto);
            entity.Id = existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;

            var saved = await _repository.SaveAsync(entity);
            return PatientMilitaryFirstResponderMapper.ToDto(saved);
        }

        public async Task DeleteAsync(PatientMilitaryFirstResponderDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            if (dto.Id != Guid.Empty)
            {
                var existingById = await _repository.GetAsync(pk, EntityKeyPolicy.Row(dto.Id))
                    ?? throw new KeyNotFoundException("Military/first responder record not found.");
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
