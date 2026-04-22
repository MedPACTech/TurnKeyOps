using MedInsights.Lib.Dtos;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using MedInsights.Lib.Utils;

namespace MedInsights.Services
{
    public sealed class TenantBillingAccountService : ITenantBillingAccountService
    {
        private readonly ITenantBillingAccountRepository _repository;
        private readonly IUserContext _userContext;

        public TenantBillingAccountService(ITenantBillingAccountRepository repository, IUserContext userContext)
        {
            _repository = repository;
            _userContext = userContext;
        }

        public async Task<TenantBillingAccountDto?> GetCurrentAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var entity = await _repository.GetAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), "BILLING", ct);
            return entity is null ? null : TenantBillingAccountMapper.ToDto(entity);
        }

        public async Task<TenantBillingAccountDto> UpsertAsync(TenantBillingAccountDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var tenantId = _userContext.TenantId;
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            const string rowKey = "BILLING";
            var existing = await _repository.GetAsync(partitionKey, rowKey, ct);

            dto.Id = existing?.Id ?? (dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id);
            dto.TenantId = tenantId;
            dto.DateCreated ??= existing?.DateCreated ?? DateTime.UtcNow;
            dto.DateUpdated = DateTime.UtcNow;

            var entity = TenantBillingAccountMapper.ToEntity(dto, partitionKey, rowKey);
            if (existing is not null)
            {
                entity.ETag = existing.ETag;
                entity.Timestamp = existing.Timestamp;
            }

            var saved = await _repository.SaveAsync(entity, ct);
            return TenantBillingAccountMapper.ToDto(saved);
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }
    }
}
