using MedInsights.Lib.Dtos;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using MedInsights.Lib.Utils;

namespace MedInsights.Services
{
    public sealed class TenantSubscriptionService : ITenantSubscriptionService
    {
        private readonly ITenantSubscriptionRepository _repository;
        private readonly IUserContext _userContext;

        public TenantSubscriptionService(ITenantSubscriptionRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        public async Task<IEnumerable<TenantSubscriptionDto>> GetAllAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var page = await _repository.GetByPartitionPagedAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), 100, ct: ct);
            return page.Results.Select(TenantSubscriptionMapper.ToDto);
        }

        public async Task<TenantSubscriptionDto?> GetAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var entity = await _repository.GetAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), EntityKeyPolicy.Row(id), ct);
            return entity is null ? null : TenantSubscriptionMapper.ToDto(entity);
        }

        public async Task<TenantSubscriptionDto> UpsertAsync(TenantSubscriptionDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var tenantId = _userContext.TenantId;
            var entityId = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var rowKey = EntityKeyPolicy.Row(entityId);
            var existing = await _repository.GetAsync(partitionKey, rowKey, ct);

            dto.Id = entityId;
            dto.TenantId = tenantId;
            dto.DateCreated ??= existing?.DateCreated ?? DateTime.UtcNow;
            dto.DateUpdated = DateTime.UtcNow;

            var entity = TenantSubscriptionMapper.ToEntity(dto, partitionKey, rowKey);
            if (existing is not null)
            {
                entity.ETag = existing.ETag;
                entity.Timestamp = existing.Timestamp;
            }

            var saved = await _repository.SaveAsync(entity, ct);
            return TenantSubscriptionMapper.ToDto(saved);
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }
    }
}
