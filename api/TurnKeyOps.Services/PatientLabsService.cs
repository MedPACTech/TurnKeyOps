using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientLabsService : IPatientLabsService
    {
        private readonly IPatientLabsRepository _repository;
        private readonly IUserContext _userContext;

        public PatientLabsService(IPatientLabsRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        public async Task<PatientLabsDto?> GetAsync(Guid patientId, Guid labId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(labId);
            var item = await _repository.GetAsync(pk, rowKey);

            return item == null ? null : PatientLabsMapper.ToDto(item);
        }

        public async Task<IEnumerable<PatientLabsDto>> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var items = await _repository.GetByPatientAsync(pk);

            return items
                .OrderByDescending(x => x.DateLabCompleted)
                .ThenByDescending(x => x.DateUploaded ?? DateTime.MinValue)
                .Select(PatientLabsMapper.ToDto);
        }

        public async Task<PatientLabsDto> AddAsync(PatientLabsDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var entity = PatientLabsMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForPatient(dto.PatientId);
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            entity.DateUploaded ??= DateTime.UtcNow;

            var saved = await _repository.SaveAsync(entity);
            return PatientLabsMapper.ToDto(saved);
        }

        public async Task<PatientLabsDto> UpdateAsync(PatientLabsDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Lab record not found.");

            var entity = PatientLabsMapper.ToEntity(dto);
            entity.Id = existing.Id == Guid.Empty ? dto.Id : existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;
            entity.DateUploaded ??= existing.DateUploaded;

            var saved = await _repository.SaveAsync(entity);
            return PatientLabsMapper.ToDto(saved);
        }

        public async Task DeleteAsync(PatientLabsDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Lab record not found.");
            existing.IsDeleted = true;
            await _repository.SaveAsync(existing);
        }
    }
}
