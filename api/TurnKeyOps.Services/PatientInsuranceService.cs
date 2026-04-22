using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientInsuranceService : IPatientInsuranceService
    {
        private readonly IPatientInsuranceRepository _repository;
        private readonly IUserContext _userContext;

        public PatientInsuranceService(IPatientInsuranceRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        private string PartitionKeyForPatient(Guid patientId) => EntityKeyPolicy.TenantPatientPartition(_userContext.TenantId, patientId);

        public async Task<PatientInsuranceDto?> GetAsync(Guid patientId, Guid insuranceId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var rowKey = EntityKeyPolicy.Row(insuranceId);
            var item = await _repository.GetAsync(pk, rowKey);

            return item == null ? null : PatientInsuranceMapper.ToDto(item);
        }

        public async Task<IEnumerable<PatientInsuranceDto>> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(patientId);
            var items = await _repository.GetByPatientAsync(pk);
            return items
                .OrderByDescending(x => x.VerificationDate)
                .Select(PatientInsuranceMapper.ToDto);
        }

        public async Task<PatientInsuranceDto> AddAsync(PatientInsuranceDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var entity = PatientInsuranceMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForPatient(dto.PatientId);
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            if (entity.EffectiveDate == default) entity.EffectiveDate = DateTime.UtcNow;
            if (entity.VerificationDate == default) entity.VerificationDate = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(entity);
            return PatientInsuranceMapper.ToDto(saved);
        }

        public async Task<PatientInsuranceDto> UpdateAsync(PatientInsuranceDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Insurance record not found.");

            var entity = PatientInsuranceMapper.ToEntity(dto);
            entity.Id = existing.Id == Guid.Empty ? dto.Id : existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;
            if (entity.EffectiveDate == default) entity.EffectiveDate = existing.EffectiveDate;
            if (entity.VerificationDate == default) entity.VerificationDate = existing.VerificationDate;

            var saved = await _repository.SaveAsync(entity);
            return PatientInsuranceMapper.ToDto(saved);
        }

        public async Task DeleteAsync(PatientInsuranceDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForPatient(dto.PatientId);
            var rowKey = EntityKeyPolicy.Row(dto.Id);
            var existing = await _repository.GetAsync(pk, rowKey)
                ?? throw new KeyNotFoundException("Insurance record not found.");
            existing.IsDeleted = true;
            await _repository.SaveAsync(existing);
        }
    }
}
