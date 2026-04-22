using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public class PatientOrderService : IPatientOrderService
    {
        private readonly IPatientOrderRepository _repository;
        private readonly IUserContext _userContext;

        public PatientOrderService(IPatientOrderRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        private string PartitionKeyForTenant() => EntityKeyPolicy.TenantPartition(_userContext.TenantId);

        public async Task<PatientOrderDto?> GetAsync(Guid orderId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var item = await _repository.GetAsync(PartitionKeyForTenant(), EntityKeyPolicy.Row(orderId));
            return item == null ? null : PatientOrderMapper.ToDto(item);
        }

        public async Task<IEnumerable<PatientOrderDto>> GetByPatientAsync(Guid patientId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var items = await _repository.GetByPatientAsync(PartitionKeyForTenant(), patientId);
            return items
                .OrderByDescending(x => x.DateOrdered)
                .Select(PatientOrderMapper.ToDto);
        }

        public async Task<IEnumerable<PatientOrderDto>> GetByProviderAsync(Guid providerId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var items = await _repository.GetByProviderAsync(PartitionKeyForTenant(), providerId);
            return items
                .OrderByDescending(x => x.DateOrdered)
                .Select(PatientOrderMapper.ToDto);
        }

        public async Task<PatientOrderDto> AddAsync(PatientOrderDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var entity = PatientOrderMapper.ToEntity(dto);
            entity.Id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
            entity.PartitionKey = PartitionKeyForTenant();
            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            if (entity.DateOrdered == default) entity.DateOrdered = DateOnly.FromDateTime(DateTime.UtcNow);

            var saved = await _repository.SaveAsync(entity);
            return PatientOrderMapper.ToDto(saved);
        }

        public async Task<PatientOrderDto> UpdateAsync(PatientOrderDto dto)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForTenant();
            var existing = await _repository.GetAsync(pk, EntityKeyPolicy.Row(dto.Id))
                ?? throw new KeyNotFoundException("Order not found.");

            var entity = PatientOrderMapper.ToEntity(dto);
            entity.Id = existing.Id == Guid.Empty ? dto.Id : existing.Id;
            entity.PartitionKey = existing.PartitionKey;
            entity.RowKey = existing.RowKey;
            entity.ETag = existing.ETag;
            entity.Timestamp = existing.Timestamp;
            if (entity.DateOrdered == default) entity.DateOrdered = existing.DateOrdered;

            var saved = await _repository.SaveAsync(entity);
            return PatientOrderMapper.ToDto(saved);
        }

        public async Task DeleteAsync(Guid orderId)
        {
            if (!_userContext.IsAuthenticated) throw new UnauthorizedAccessException();

            var pk = PartitionKeyForTenant();
            var existing = await _repository.GetAsync(pk, EntityKeyPolicy.Row(orderId))
                ?? throw new KeyNotFoundException("Order not found.");

            existing.IsDeleted = true;
            await _repository.SaveAsync(existing);
        }
    }
}
