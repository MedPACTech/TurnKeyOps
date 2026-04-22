using MedInsights.Lib;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class TenantMembershipAuthorizationService : ITenantMembershipAuthorizationService
    {
        private readonly ITenantMembershipRepository _membershipRepository;
        private readonly IUserContext _userContext;

        public TenantMembershipAuthorizationService(
            ITenantMembershipRepository membershipRepository,
            IUserContext userContext)
        {
            _membershipRepository = membershipRepository;
            _userContext = userContext;
        }

        public async Task RequireBillingAccessAsync(CancellationToken ct = default)
        {
            var membership = await GetCurrentMembershipOrThrowAsync(ct);
            if (!membership.IsOwner && !membership.IsBillingAdmin)
                throw new ForbiddenAccessException("Current user cannot make billing changes.");
        }

        public async Task RequireMembershipManagementAccessAsync(CancellationToken ct = default)
        {
            var membership = await GetCurrentMembershipOrThrowAsync(ct);
            if (!membership.IsOwner && !TenantRoleCatalog.CanManageRoles(membership.Role))
                throw new ForbiddenAccessException("Current user cannot manage tenant memberships.");
        }

        private async Task<MedInsights.Lib.Entities.TenantMembership> GetCurrentMembershipOrThrowAsync(CancellationToken ct)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            return await _membershipRepository.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), _userContext.UserId, ct)
                   ?? throw new ForbiddenAccessException("Current user is not an active tenant member.");
        }
    }
}
