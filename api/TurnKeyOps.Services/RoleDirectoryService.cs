using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class RoleDirectoryService : IRoleDirectoryService
    {
        private readonly ITenantRoleDefinitionRepository _roleRepository;

        public RoleDirectoryService(ITenantRoleDefinitionRepository roleRepository)
        {
            _roleRepository = roleRepository;
        }

        public async Task<TenantRoleDefinition?> GetRoleAsync(Guid tenantId, string roleKey, CancellationToken ct = default)
        {
            var normalized = NormalizeRoleKey(roleKey);
            if (string.IsNullOrWhiteSpace(normalized))
                return null;

            return await _roleRepository.GetTenantByKeyAsync(tenantId, normalized, ct)
                ?? await _roleRepository.GetSystemByKeyAsync(normalized, ct);
        }

        public async Task<TenantRoleDefinition> GetRequiredAssignableRoleAsync(Guid tenantId, string roleKey, CancellationToken ct = default)
        {
            var role = await GetRoleAsync(tenantId, roleKey, ct)
                ?? throw new ArgumentException("Role is invalid.", nameof(roleKey));

            if (!role.IsAssignable)
                throw new ArgumentException("Role is invalid or not assignable.", nameof(roleKey));

            return role;
        }

        public string NormalizeRoleKey(string roleKey)
        {
            if (string.IsNullOrWhiteSpace(roleKey))
                throw new ArgumentException("Role is required.", nameof(roleKey));

            var normalized = roleKey.Trim().ToLowerInvariant();
            return normalized switch
            {
                "billing admin" => TenantRoleCatalog.BillingAdmin,
                "billing-admin" => TenantRoleCatalog.BillingAdmin,
                "billingadmin" => TenantRoleCatalog.BillingAdmin,
                _ => normalized.Replace(' ', '_').Replace('-', '_')
            };
        }
    }
}
