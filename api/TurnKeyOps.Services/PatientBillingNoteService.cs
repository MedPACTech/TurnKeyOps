using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientBillingNoteService : IPatientBillingNoteService
    {
        private readonly IPatientBillingNoteRepository _repository;
        private readonly IUserContext _userContext;

        public PatientBillingNoteService(IPatientBillingNoteRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        public async Task<PatientBillingNoteDto?> GetAsync(Guid patientId, Guid billingNoteId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(billingNoteId);
            var item = await _repository.GetAsync(pk, rowKey);

            return item == null ? null : PatientBillingNoteMapper.ToDto(item);
        }

        public async Task<IEnumerable<PatientBillingNoteDto>> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var items = await _repository.GetByPatientAsync(pk);

            return items.Select(PatientBillingNoteMapper.ToDto);
        }

        public async Task<PatientBillingNoteDto> AddAsync(PatientBillingNoteDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var entity = PatientBillingNoteMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForPatient(dto.PatientId);
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            if (entity.DateCreated == default) entity.DateCreated = DateTime.UtcNow;
            entity.DateUpdated = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(entity);
            return PatientBillingNoteMapper.ToDto(saved);
        }

        public async Task<PatientBillingNoteDto> UpdateAsync(PatientBillingNoteDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Billing note not found.");

            var entity = PatientBillingNoteMapper.ToEntity(dto);
            entity.Id = existing.Id == Guid.Empty ? dto.Id : existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;
            entity.DateCreated = existing.DateCreated == default ? entity.DateCreated : existing.DateCreated;
            entity.DateUpdated = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(entity);
            return PatientBillingNoteMapper.ToDto(saved);
        }

        public async Task DeleteAsync(Guid id)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var existing = await _repository.GetByRowKeyAsync(EntityKeyPolicy.Row(id), CancellationToken.None)
                ?? throw new KeyNotFoundException("Billing note not found.");

            existing.IsDeleted = true;
            existing.DateUpdated = DateTime.UtcNow;
            await _repository.SaveAsync(existing);
        }
    }
}
