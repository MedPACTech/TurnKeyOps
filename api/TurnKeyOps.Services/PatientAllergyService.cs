using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientAllergyService : IPatientAllergyService
    {
        private readonly IPatientAllergyRepository _repository;
        private readonly IUserContext _userContext;

        public PatientAllergyService(
            IPatientAllergyRepository repository,
            IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        public PatientAllergyService(
            IPatientAllergyRepository repository,
            IUserContext userContext,
            IRoleAccessService roleAccessService)
            : this(repository, userContext)
        {
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        public async Task<PatientAllergyDto?> GetAsync(Guid patientId, Guid allergyId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(allergyId);
            var item = await _repository.GetAsync(pk, rowKey);

            return item == null ? null : PatientAllergyMapper.ToDto(item);
        }

        public async Task<IEnumerable<PatientAllergyDto>> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var items = await _repository.GetByPatientAsync(pk);

            return items
                .OrderByDescending(x => x.DateNoted)
                .Select(PatientAllergyMapper.ToDto);
        }

        public async Task<PatientAllergyDto> AddAsync(PatientAllergyDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var entity = PatientAllergyMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForPatient(dto.PatientId);
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            if (entity.DateNoted == default) entity.DateNoted = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(entity);
            return PatientAllergyMapper.ToDto(saved);
        }

        public async Task<PatientAllergyDto> UpdateAsync(PatientAllergyDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Allergy record not found.");

            var entity = PatientAllergyMapper.ToEntity(dto);
            entity.Id = existing.Id == Guid.Empty ? dto.Id : existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;
            if (entity.DateNoted == default) entity.DateNoted = existing.DateNoted;

            var saved = await _repository.SaveAsync(entity);
            return PatientAllergyMapper.ToDto(saved);
        }

        public async Task DeleteAsync(PatientAllergyDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Allergy record not found.");
            existing.IsDeleted = true;
            await _repository.SaveAsync(existing);
        }
    }
}
