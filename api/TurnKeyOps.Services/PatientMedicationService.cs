using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientMedicationService : IPatientMedicationService
    {
        private readonly IPatientMedicationRepository _repository;
        private readonly IUserContext _userContext;

        public PatientMedicationService(IPatientMedicationRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        private string PartitionKeyForTenant() => EntityKeyPolicy.TenantPartition(_userContext.TenantId);

        public async Task<PatientMedicationDto?> GetAsync(Guid medicationRecordId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var item = await _repository.GetAsync(PartitionKeyForTenant(), EntityKeyPolicy.Row(medicationRecordId));
            return item == null ? null : PatientMedicationMapper.ToDto(item);
        }

        public async Task<IEnumerable<PatientMedicationDto>> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var items = await _repository.GetByPatientAsync(PartitionKeyForTenant(), patientId);
            return items
                .OrderByDescending(x => x.DateNoted)
                .Select(PatientMedicationMapper.ToDto);
        }

        public async Task<IEnumerable<PatientMedicationDto>> GetByProviderAsync(Guid providerId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var items = await _repository.GetByProviderAsync(PartitionKeyForTenant(), providerId);
            return items
                .OrderByDescending(x => x.DateNoted)
                .Select(PatientMedicationMapper.ToDto);
        }

        public async Task<PatientMedicationDto> AddAsync(PatientMedicationDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var entity = PatientMedicationMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForTenant();
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            if (entity.DateNoted == default) entity.DateNoted = DateTime.UtcNow;
            if (!entity.IsEnded) entity.DateEnded = default;

            var saved = await _repository.SaveAsync(entity);
            return PatientMedicationMapper.ToDto(saved);
        }

        public async Task<PatientMedicationDto> UpdateAsync(PatientMedicationDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForTenant();
            var existing = await _repository.GetAsync(pk, EntityKeyPolicy.Row(dto.Id))
                ?? throw new KeyNotFoundException("Medication record not found.");

            var entity = PatientMedicationMapper.ToEntity(dto);
            entity.Id = existing.Id == Guid.Empty ? dto.Id : existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;
            if (entity.DateNoted == default) entity.DateNoted = existing.DateNoted;
            if (!entity.IsEnded) entity.DateEnded = default;

            var saved = await _repository.SaveAsync(entity);
            return PatientMedicationMapper.ToDto(saved);
        }

        public async Task DeleteAsync(Guid medicationRecordId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForTenant();
            var existing = await _repository.GetAsync(pk, EntityKeyPolicy.Row(medicationRecordId))
                ?? throw new KeyNotFoundException("Medication record not found.");

            existing.IsDeleted = true;
            await _repository.SaveAsync(existing);
        }
    }
}
