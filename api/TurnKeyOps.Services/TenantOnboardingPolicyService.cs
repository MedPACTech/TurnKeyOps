using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class TenantOnboardingPolicyService : ITenantOnboardingPolicyService
    {
        private const string RowKey = "ONBOARDING";
        private readonly ITenantOnboardingPolicyRepository _repository;
        private readonly IUserContext _userContext;
        private readonly ITenantMembershipAuthorizationService _membershipAuthorizationService;

        public TenantOnboardingPolicyService(
            ITenantOnboardingPolicyRepository repository,
            IUserContext userContext,
            ITenantMembershipAuthorizationService membershipAuthorizationService)
        {
            _repository = repository;
            _userContext = userContext;
            _membershipAuthorizationService = membershipAuthorizationService;
        }

        public async Task<TenantOnboardingPolicyDto> GetCurrentAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            return await GetByTenantAsync(_userContext.TenantId, ct);
        }

        public async Task<TenantOnboardingPolicyDto> GetByTenantAsync(Guid tenantId, CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));

            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var entity = await _repository.GetAsync(partitionKey, RowKey, ct);
            if (entity is null)
            {
                entity = await _repository.SaveAsync(new TenantOnboardingPolicy
                {
                    Id = tenantId,
                    TenantId = tenantId,
                    PartitionKey = partitionKey,
                    RowKey = RowKey,
                    ReserveSeatAtInviteTime = true,
                    AutoAssignSeatOnActivation = true,
                    DefaultInviteExpiryDays = TenantOnboardingPolicyDefaults.DefaultInviteExpiryDays,
                    ExpiredInviteHandling = TenantOnboardingPolicyDefaults.CancelAndReleaseSeat,
                    DateCreated = DateTime.UtcNow,
                    DateUpdated = DateTime.UtcNow,
                    IsDeleted = false
                }, ct);
            }

            Normalize(entity);
            return TenantOnboardingPolicyMapper.ToDto(entity);
        }

        public async Task<TenantOnboardingPolicyDto> UpdateCurrentAsync(TenantOnboardingPolicyDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireBillingAccessAsync(ct);

            var tenantId = _userContext.TenantId;
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var existing = await _repository.GetAsync(partitionKey, RowKey, ct);
            var entity = existing ?? new TenantOnboardingPolicy
            {
                Id = tenantId,
                TenantId = tenantId,
                PartitionKey = partitionKey,
                RowKey = RowKey,
                DateCreated = DateTime.UtcNow,
                IsDeleted = false
            };

            entity.ReserveSeatAtInviteTime = dto.ReserveSeatAtInviteTime;
            entity.AutoAssignSeatOnActivation = dto.AutoAssignSeatOnActivation;
            entity.DefaultInviteExpiryDays = Math.Clamp(dto.DefaultInviteExpiryDays, 1, 90);
            entity.ExpiredInviteHandling = NormalizeExpiredInviteHandling(dto.ExpiredInviteHandling);
            entity.DateUpdated = DateTime.UtcNow;
            if (existing is not null)
            {
                entity.ETag = existing.ETag;
                entity.Timestamp = existing.Timestamp;
            }

            var saved = await _repository.SaveAsync(entity, ct);
            return TenantOnboardingPolicyMapper.ToDto(saved);
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private static void Normalize(TenantOnboardingPolicy entity)
        {
            entity.DefaultInviteExpiryDays = Math.Clamp(entity.DefaultInviteExpiryDays <= 0 ? TenantOnboardingPolicyDefaults.DefaultInviteExpiryDays : entity.DefaultInviteExpiryDays, 1, 90);
            entity.ExpiredInviteHandling = NormalizeExpiredInviteHandling(entity.ExpiredInviteHandling);
        }

        private static string NormalizeExpiredInviteHandling(string? value)
        {
            var normalized = string.IsNullOrWhiteSpace(value)
                ? TenantOnboardingPolicyDefaults.CancelAndReleaseSeat
                : value.Trim().ToLowerInvariant();

            return normalized switch
            {
                TenantOnboardingPolicyDefaults.CancelAndReleaseSeat => normalized,
                TenantOnboardingPolicyDefaults.MarkExpiredKeepSeat => normalized,
                _ => throw new ArgumentException("ExpiredInviteHandling is invalid.", nameof(value))
            };
        }
    }
}
