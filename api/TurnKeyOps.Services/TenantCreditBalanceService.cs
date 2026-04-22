using MedInsights.Lib.Dtos;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using MedInsights.Lib.Utils;

namespace MedInsights.Services
{
    public sealed class TenantCreditBalanceService : ITenantCreditBalanceService
    {
        private readonly ITenantCreditBalanceRepository _repository;
        private readonly ITenantSubscriptionRepository _subscriptionRepository;
        private readonly IUserContext _userContext;
        private readonly ITenantMembershipAuthorizationService _membershipAuthorizationService;

        public TenantCreditBalanceService(
            ITenantCreditBalanceRepository repository,
            ITenantSubscriptionRepository subscriptionRepository,
            ITenantMembershipAuthorizationService membershipAuthorizationService,
            IUserContext userContext)
        {
            _repository = repository;
            _subscriptionRepository = subscriptionRepository;
            _membershipAuthorizationService = membershipAuthorizationService;
            _userContext = userContext;
        }

        public async Task<TenantCreditBalanceDto?> GetCurrentAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var partitionKey = EntityKeyPolicy.TenantPartition(_userContext.TenantId);
            var entity = await _repository.GetAsync(partitionKey, "CREDITS", ct);
            if (entity is null)
                return null;

            if (await NormalizeEntityAsync(entity, ct))
                entity = await _repository.SaveAsync(entity, ct);

            return entity is null ? null : TenantCreditBalanceMapper.ToDto(entity);
        }

        public async Task<TenantCreditBalanceDto> UpsertAsync(TenantCreditBalanceDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireBillingAccessAsync(ct);

            var tenantId = _userContext.TenantId;
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            const string rowKey = "CREDITS";
            var existing = await _repository.GetAsync(partitionKey, rowKey, ct);

            dto.Id = existing?.Id ?? (dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id);
            dto.TenantId = tenantId;
            dto.DateCreated ??= existing?.DateCreated ?? DateTime.UtcNow;
            dto.DateUpdated = DateTime.UtcNow;
            await NormalizeDtoAsync(dto, existing, ct);

            var entity = TenantCreditBalanceMapper.ToEntity(dto, partitionKey, rowKey);
            if (existing is not null)
            {
                entity.ETag = existing.ETag;
                entity.Timestamp = existing.Timestamp;
            }

            var saved = await _repository.SaveAsync(entity, ct);
            return TenantCreditBalanceMapper.ToDto(saved);
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private async Task NormalizeDtoAsync(TenantCreditBalanceDto dto, Lib.Entities.TenantCreditBalance? existing, CancellationToken ct)
        {
            var usageWindow = await ResolveUsageWindowAsync(ct);

            if (!HasValidUsageWindow(dto.CurrentUsagePeriodStartUtc, dto.CurrentUsagePeriodEndUtc))
            {
                dto.CurrentUsagePeriodStartUtc = existing is not null && HasValidUsageWindow(existing.CurrentUsagePeriodStartUtc, existing.CurrentUsagePeriodEndUtc)
                    ? existing.CurrentUsagePeriodStartUtc
                    : usageWindow.StartUtc;
                dto.CurrentUsagePeriodEndUtc = existing is not null && HasValidUsageWindow(existing.CurrentUsagePeriodStartUtc, existing.CurrentUsagePeriodEndUtc)
                    ? existing.CurrentUsagePeriodEndUtc
                    : usageWindow.EndUtc;
            }

            if (dto.PurchasedCreditsExpireAtUtc <= dto.CurrentUsagePeriodStartUtc)
                dto.PurchasedCreditsExpireAtUtc = dto.CurrentUsagePeriodEndUtc;
        }

        private async Task<bool> NormalizeEntityAsync(Lib.Entities.TenantCreditBalance entity, CancellationToken ct)
        {
            var dirty = false;

            if (!HasValidUsageWindow(entity.CurrentUsagePeriodStartUtc, entity.CurrentUsagePeriodEndUtc))
            {
                var usageWindow = await ResolveUsageWindowAsync(ct);
                entity.CurrentUsagePeriodStartUtc = usageWindow.StartUtc;
                entity.CurrentUsagePeriodEndUtc = usageWindow.EndUtc;
                dirty = true;
            }

            if (entity.PurchasedCreditsExpireAtUtc <= entity.CurrentUsagePeriodStartUtc)
            {
                entity.PurchasedCreditsExpireAtUtc = entity.CurrentUsagePeriodEndUtc;
                dirty = true;
            }

            if (dirty)
                entity.DateUpdated = DateTime.UtcNow;

            return dirty;
        }

        private async Task<(DateTime StartUtc, DateTime EndUtc)> ResolveUsageWindowAsync(CancellationToken ct)
        {
            var subscription = await _subscriptionRepository.GetCurrentAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), ct);
            if (subscription is not null && HasValidUsageWindow(subscription.TermStartUtc, subscription.TermEndUtc))
                return (subscription.TermStartUtc, subscription.TermEndUtc);

            var now = DateTime.UtcNow;
            var monthStartUtc = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return (monthStartUtc, monthStartUtc.AddMonths(1));
        }

        private static bool HasValidUsageWindow(DateTime startUtc, DateTime endUtc)
            => startUtc != default
               && endUtc != default
               && endUtc > startUtc;
    }
}
