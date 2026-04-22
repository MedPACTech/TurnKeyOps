using MedInsights.Lib.Dtos;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;
using MedInsights.Lib.Utils;

namespace MedInsights.Services
{
    public sealed class TenantMembershipService : ITenantMembershipService
    {
        private readonly ITenantMembershipRepository _repository;
        private readonly ITenantSeatEntitlementService _seatEntitlementService;
        private readonly IInviteService _inviteService;
        private readonly IUserContext _userContext;
        private readonly IAuditService _auditService;
        private readonly ITenantMembershipAuthorizationService _membershipAuthorizationService;
        private readonly IRoleDirectoryService _roleDirectoryService;

        public TenantMembershipService(
            ITenantMembershipRepository repository,
            ITenantSeatEntitlementService seatEntitlementService,
            IInviteService inviteService,
            IUserContext userContext,
            IAuditService auditService,
            ITenantMembershipAuthorizationService membershipAuthorizationService,
            IRoleDirectoryService roleDirectoryService)
        {
            _repository = repository;
            _seatEntitlementService = seatEntitlementService;
            _inviteService = inviteService;
            _userContext = userContext;
            _auditService = auditService;
            _membershipAuthorizationService = membershipAuthorizationService;
            _roleDirectoryService = roleDirectoryService;
        }

        public async Task<IEnumerable<TenantMembershipDto>> GetAllAsync(CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var page = await _repository.GetByPartitionPagedAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), 200, ct: ct);
            return page.Results.Select(TenantMembershipMapper.ToDto);
        }

        public async Task<TenantMembershipDto?> GetAsync(Guid id, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var entity = await _repository.GetAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), EntityKeyPolicy.Row(id), ct);
            return entity is null ? null : TenantMembershipMapper.ToDto(entity);
        }

        public async Task<TenantMembershipDto> UpsertAsync(TenantMembershipDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var tenantId = _userContext.TenantId;
            var entityId = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;
            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var rowKey = EntityKeyPolicy.Row(entityId);
            var existing = await _repository.GetAsync(partitionKey, rowKey, ct);

            dto.Id = entityId;
            dto.TenantId = tenantId;
            dto.DateCreated ??= existing?.DateCreated ?? DateTime.UtcNow;
            dto.DateUpdated = DateTime.UtcNow;
            await ApplyRoleAsync(dto, existing?.IsOwner ?? dto.IsOwner, ct);

            var entity = TenantMembershipMapper.ToEntity(dto, partitionKey, rowKey);
            if (existing is not null)
            {
                entity.ETag = existing.ETag;
                entity.Timestamp = existing.Timestamp;
            }

            var saved = await _repository.SaveAsync(entity, ct);
            return TenantMembershipMapper.ToDto(saved);
        }

        public async Task<TenantMembershipDto> UpdateRoleAsync(Guid membershipId, UpdateMembershipRoleRequestDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var membership = await _repository.GetAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), EntityKeyPolicy.Row(membershipId), ct)
                ?? throw new KeyNotFoundException("Membership not found.");

            if (membership.IsOwner)
                throw new InvalidOperationException("Owner membership role cannot be changed.");

            var role = await _roleDirectoryService.GetRequiredAssignableRoleAsync(_userContext.TenantId, dto.Role, ct);
            membership.Role = role.Key;
            membership.IsBillingAdmin = role.GrantsBillingAdmin;
            membership.DateUpdated = DateTime.UtcNow;

            var saved = await _repository.SaveAsync(membership, ct);
            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                Category = "admin",
                Action = "tenant_membership_role_updated",
                Severity = "info",
                TargetType = "tenant_membership",
                TargetId = membership.Id.ToString("D"),
                Source = nameof(TenantMembershipService),
                Description = $"Updated tenant membership role to '{role.Key}'."
            }, ct);

            return TenantMembershipMapper.ToDto(saved);
        }

        public async Task<TenantMembershipDto> ReassignAsync(Guid membershipId, ReassignMembershipRequestDto dto, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);
            ValidateContact(dto.InvitedEmail, dto.InvitedPhone);

            var membership = await _repository.GetAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), EntityKeyPolicy.Row(membershipId), ct)
                ?? throw new KeyNotFoundException("Membership not found.");

            if (membership.IsOwner)
                throw new InvalidOperationException("Owner membership cannot be reassigned.");

            var previousSeatStatus = membership.SeatStatus;
            membership.MembershipStatus = "Removed";
            membership.SeatStatus = "Released";
            membership.DateRemoved = DateTime.UtcNow;
            membership.DateUpdated = DateTime.UtcNow;
            await _repository.SaveAsync(membership, ct);
            await _seatEntitlementService.ReleaseSeatAsync(_userContext.TenantId, previousSeatStatus, ct);

            await _inviteService.CreateAsync(new CreateInviteRequestDto
            {
                InvitedEmail = dto.InvitedEmail,
                InvitedPhone = dto.InvitedPhone,
                Role = string.IsNullOrWhiteSpace(dto.Role) ? membership.Role : dto.Role,
                ExpiresAtUtc = dto.ExpiresAtUtc
            }, ct);

            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                Category = "admin",
                Action = "tenant_membership_reassigned",
                Severity = "info",
                TargetType = "tenant_membership",
                TargetId = membership.Id.ToString("D"),
                Source = nameof(TenantMembershipService),
                Description = "Reassigned tenant membership through invite workflow."
            }, ct);

            return TenantMembershipMapper.ToDto(membership);
        }

        public async Task<TenantMembershipDto> RemoveAsync(Guid membershipId, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            await _membershipAuthorizationService.RequireMembershipManagementAccessAsync(ct);

            var membership = await _repository.GetAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), EntityKeyPolicy.Row(membershipId), ct)
                ?? throw new KeyNotFoundException("Membership not found.");

            if (membership.IsOwner)
                throw new InvalidOperationException("Owner membership cannot be removed.");

            var previousSeatStatus = membership.SeatStatus;
            membership.MembershipStatus = "Removed";
            membership.SeatStatus = "Released";
            membership.DateRemoved = DateTime.UtcNow;
            membership.DateUpdated = DateTime.UtcNow;

            await _repository.SaveAsync(membership, ct);
            await _seatEntitlementService.ReleaseSeatAsync(_userContext.TenantId, previousSeatStatus, ct);

            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                Category = "admin",
                Action = "tenant_membership_removed",
                Severity = "info",
                TargetType = "tenant_membership",
                TargetId = membership.Id.ToString("D"),
                Source = nameof(TenantMembershipService),
                Description = "Removed tenant membership and released seat."
            }, ct);

            return TenantMembershipMapper.ToDto(membership);
        }

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }

        private static void ValidateContact(string? email, string? phone)
        {
            if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
                throw new ArgumentException("Either invited email or invited phone is required.");
        }

        private async Task ApplyRoleAsync(TenantMembershipDto dto, bool isOwner, CancellationToken ct)
        {
            if (isOwner)
            {
                dto.Role = TenantRoleCatalog.Owner;
                dto.IsOwner = true;
                dto.IsBillingAdmin = true;
                return;
            }

            var role = await _roleDirectoryService.GetRequiredAssignableRoleAsync(_userContext.TenantId, dto.Role, ct);
            dto.Role = role.Key;
            dto.IsOwner = false;
            dto.IsBillingAdmin = role.GrantsBillingAdmin;
        }
    }
}
