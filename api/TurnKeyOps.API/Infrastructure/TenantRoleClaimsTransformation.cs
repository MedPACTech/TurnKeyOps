using Claim = System.Security.Claims.Claim;
using System.Security.Claims;
using MedInsights.Lib;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using MedInsights.Lib.Authorization;

namespace MedInsights.API.Infrastructure
{
    public sealed class TenantRoleClaimsTransformation : IClaimsTransformation
    {
        private readonly ITenantMembershipRepository _membershipRepository;
        private readonly IRoleDirectoryService _roleDirectoryService;

        public TenantRoleClaimsTransformation(
            ITenantMembershipRepository membershipRepository,
            IRoleDirectoryService roleDirectoryService)
        {
            _membershipRepository = membershipRepository;
            _roleDirectoryService = roleDirectoryService;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated != true)
                return principal;

            var identity = principal.Identity as ClaimsIdentity;
            if (identity is null)
                return principal;

            if (principal.HasClaim(TurnKeyAuthorizationPolicies.ClaimsTransformedMarker, "true"))
                return principal;

            var isInternalAdmin = principal.FindAll("role")
                    .Concat(principal.FindAll(ClaimTypes.Role))
                    .Any(claim => string.Equals(NormalizeRole(claim.Value), TurnKeyAuthorizationRoles.InternalAdmin, StringComparison.Ordinal));

            RemoveClaims(identity, "role");
            RemoveClaims(identity, ClaimTypes.Role);
            RemoveClaims(identity, TurnKeyAuthorizationPolicies.TenantRoleClaimType);
            RemoveClaims(identity, "rid");
            RemoveClaims(identity, "role_id");

            if (isInternalAdmin)
            {
                identity.AddClaim(new Claim("role", TurnKeyAuthorizationRoles.InternalAdmin, ClaimValueTypes.String, "identity-provider"));
                identity.AddClaim(new Claim(ClaimTypes.Role, TurnKeyAuthorizationRoles.InternalAdmin, ClaimValueTypes.String, "identity-provider"));
            }

            if (!TryGetGuid(principal, out var tenantId, "tenant_id", "tenant", "tid", "http://schemas.microsoft.com/identity/claims/tenantid")
                || !TryGetGuid(principal, out var userId, ClaimTypes.NameIdentifier, "uid", "sub"))
            {
                identity.AddClaim(new Claim(TurnKeyAuthorizationPolicies.ClaimsTransformedMarker, "true"));
                return principal;
            }

            var membership = await _membershipRepository.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(tenantId), userId);
            if (membership is null || !IsActive(membership) || string.IsNullOrWhiteSpace(membership.Role))
            {
                identity.AddClaim(new Claim(TurnKeyAuthorizationPolicies.ClaimsTransformedMarker, "true"));
                return principal;
            }

            var role = await _roleDirectoryService.GetRoleAsync(tenantId, membership.Role);
            var roleKey = _roleDirectoryService.NormalizeRoleKey(membership.Role);

            identity.AddClaim(new Claim("role", roleKey, ClaimValueTypes.String, "tenant-membership"));
            identity.AddClaim(new Claim(ClaimTypes.Role, roleKey, ClaimValueTypes.String, "tenant-membership"));
            identity.AddClaim(new Claim(TurnKeyAuthorizationPolicies.TenantRoleClaimType, roleKey, ClaimValueTypes.String, "tenant-membership"));

            if (role is not null)
            {
                var roleId = role.Id.ToString("D");
                identity.AddClaim(new Claim("rid", roleId, ClaimValueTypes.String, "tenant-membership"));
                identity.AddClaim(new Claim("role_id", roleId, ClaimValueTypes.String, "tenant-membership"));
            }

            identity.AddClaim(new Claim(TurnKeyAuthorizationPolicies.ClaimsTransformedMarker, "true"));

            return principal;
        }

        private static string NormalizeRole(string value)
            => value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

        private static bool IsActive(MedInsights.Lib.Entities.TenantMembership membership)
            => !membership.IsDeleted
               && !membership.DateRemoved.HasValue
               && string.Equals(membership.MembershipStatus, "active", StringComparison.OrdinalIgnoreCase);

        private static void RemoveClaims(ClaimsIdentity identity, string claimType)
        {
            foreach (var claim in identity.FindAll(claimType).ToList())
                identity.RemoveClaim(claim);
        }

        private static bool TryGetGuid(ClaimsPrincipal principal, out Guid value, params string[] claimTypes)
        {
            foreach (var claimType in claimTypes)
            {
                foreach (var claim in principal.FindAll(claimType))
                {
                    if (Guid.TryParse(claim.Value, out value) && value != Guid.Empty)
                        return true;
                }
            }

            value = Guid.Empty;
            return false;
        }
    }
}
