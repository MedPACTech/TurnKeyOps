using System.Reflection;
using System.Security.Claims;
using System.Collections;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.AspNetCore.Http;

namespace MedInsights.Services
{
    public sealed class RoleAccessService : IRoleAccessService
    {
        private readonly IUserContext _userContext;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITenantMembershipRepository _membershipRepository;
        private readonly IRolePermissionMappingRepository _mappingRepository;
        private readonly IRoleDirectoryService _roleDirectoryService;

        public RoleAccessService(
            IUserContext userContext,
            IHttpContextAccessor httpContextAccessor,
            ITenantMembershipRepository membershipRepository,
            IRolePermissionMappingRepository mappingRepository,
            IRoleDirectoryService roleDirectoryService)
        {
            _userContext = userContext;
            _httpContextAccessor = httpContextAccessor;
            _membershipRepository = membershipRepository;
            _mappingRepository = mappingRepository;
            _roleDirectoryService = roleDirectoryService;
        }

        public async Task RequirePermissionAsync(string permissionKey, CancellationToken ct = default)
        {
            if (!await HasPermissionAsync(permissionKey, ct))
                throw new ForbiddenAccessException($"Current user does not have permission '{permissionKey}'.");
        }

        public async Task RequireAnyRoleAsync(IEnumerable<string> roleKeys, CancellationToken ct = default)
        {
            if (!await HasAnyRoleAsync(roleKeys, ct))
                throw new ForbiddenAccessException("Current user does not have the required role.");
        }

        public async Task RequireAnyRoleIdAsync(IEnumerable<string> roleIds, CancellationToken ct = default)
        {
            if (!await HasAnyRoleIdAsync(roleIds, ct))
                throw new ForbiddenAccessException("Current user does not have the required role id.");
        }

        public Task<bool> HasAnyRoleAsync(IEnumerable<string> roleKeys, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var principal = _httpContextAccessor.HttpContext?.User;
            var allowed = new HashSet<string>(roleKeys.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Normalize), StringComparer.OrdinalIgnoreCase);
            if (principal is null)
                return Task.FromResult(false);

            var roles = principal.FindAll(ClaimTypes.Role).Select(x => Normalize(x.Value));
            return Task.FromResult(roles.Any(allowed.Contains));
        }

        public async Task<bool> HasPermissionAsync(string permissionKey, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var membership = await _membershipRepository.GetByUserIdAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), _userContext.UserId, ct);
            if (membership is null)
                return false;

            var role = await _roleDirectoryService.GetRoleAsync(_userContext.TenantId, membership.Role, ct);
            if (role is null)
                return false;

            var mappings = role.TenantId.HasValue
                ? await _mappingRepository.GetMappingsForRoleAsync(role.TenantId, role.Id, ct)
                : await _mappingRepository.GetMappingsForRoleAsync(null, role.Id, ct);

            return mappings.Any(x => string.Equals(x.PermissionKey, permissionKey, StringComparison.OrdinalIgnoreCase) && !x.IsDeleted);
        }

        public Task<bool> HasAnyRoleIdAsync(IEnumerable<string> roleIds, CancellationToken ct = default)
        {
            EnsureAuthenticated();
            var principal = _httpContextAccessor.HttpContext?.User;
            var allowed = new HashSet<string>(roleIds.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Normalize), StringComparer.OrdinalIgnoreCase);
            if (principal is null)
                return Task.FromResult(false);

            var ids = principal.FindAll("rid")
                .Concat(principal.FindAll("role_id"))
                .Select(x => Normalize(x.Value));

            return Task.FromResult(ids.Any(allowed.Contains));
        }

        public async Task EnforceDeclaredAccessAsync(Type targetType, string methodName, CancellationToken ct = default)
        {
            EnsureAuthenticated();

            var method = targetType.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
                ?? throw new InvalidOperationException($"Method '{methodName}' was not found on '{targetType.Name}'.");

            var methodRoleIds = GetAttributeValues(method, "AllowRoleIdsAttribute", "RoleIds", "Values", "Ids");
            if (methodRoleIds.Count > 0)
            {
                await RequireAnyRoleIdAsync(methodRoleIds, ct);
                return;
            }

            var methodRoles = GetAttributeValues(method, "AllowRolesAttribute");
            if (methodRoles.Count > 0)
            {
                await RequireAnyRoleAsync(methodRoles, ct);
                return;
            }

            var classRoleIds = GetAttributeValues(targetType, "AllowRoleIdsAttribute", "RoleIds", "Values", "Ids");
            if (classRoleIds.Count > 0)
            {
                await RequireAnyRoleIdAsync(classRoleIds, ct);
                return;
            }

            var classRoles = GetAttributeValues(targetType, "AllowRolesAttribute");
            if (classRoles.Count > 0)
            {
                await RequireAnyRoleAsync(classRoles, ct);
            }
        }

        private static List<string> GetAttributeValues(MemberInfo member, string attributeTypeName, params string[] propertyNames)
        {
            return member.GetCustomAttributes(inherit: true)
                .Where(attr => string.Equals(attr.GetType().Name, attributeTypeName, StringComparison.Ordinal))
                .SelectMany(attr =>
                {
                    var namesToSearch = propertyNames.Length == 0
                        ? ["Roles", "RoleNames", "Values"]
                        : propertyNames;
                    var property = attr.GetType().GetProperties()
                        .FirstOrDefault(x => namesToSearch.Contains(x.Name, StringComparer.OrdinalIgnoreCase));
                    if (property?.GetValue(attr) is IEnumerable<string> values)
                        return values;

                    if (property?.GetValue(attr) is IEnumerable sequence and not string)
                    {
                        return sequence.Cast<object?>()
                            .Where(x => x is not null)
                            .Select(x => x!.ToString() ?? string.Empty)
                            .Where(x => !string.IsNullOrWhiteSpace(x));
                    }

                    if (property?.GetValue(attr) is string propString && !string.IsNullOrWhiteSpace(propString))
                        return [propString];

                    var ctorProp = attr.GetType().GetProperty("Policy");
                    if (ctorProp?.GetValue(attr) is string single && !string.IsNullOrWhiteSpace(single))
                        return [single];

                    return Array.Empty<string>();
                })
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(Normalize)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string Normalize(string role) => role.Trim().ToLowerInvariant();

        private void EnsureAuthenticated()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();
        }
    }
}
