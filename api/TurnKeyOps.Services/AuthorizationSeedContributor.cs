using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Entities;
using MedInsights.Repositories;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class AuthorizationSeedContributor : IStartupSeedContributor
    {
        private readonly ITenantRoleDefinitionRepository _roleRepository;
        private readonly IRolePermissionMappingRepository _mappingRepository;
        private readonly IRolePermissionCatalog _catalog;

        public AuthorizationSeedContributor(
            ITenantRoleDefinitionRepository roleRepository,
            IRolePermissionMappingRepository mappingRepository,
            IRolePermissionCatalog catalog)
        {
            _roleRepository = roleRepository;
            _mappingRepository = mappingRepository;
            _catalog = catalog;
        }

        public async Task SeedAsync(CancellationToken ct = default)
        {
            var systemRoles = BuildSystemRoles();
            foreach (var role in systemRoles)
            {
                var existing = await _roleRepository.GetSystemByKeyAsync(role.Key, ct);
                if (existing is null)
                {
                    await _roleRepository.SaveAsync(role, ct);
                }
            }

            var permissions = _catalog.GetPermissions()
                .Select(option => new PermissionDefinitionOption
                {
                    Key = option.Key,
                    Id = option.Id ?? CreateDeterministicGuid(option.Key),
                    Name = option.Name ?? option.Key,
                    Description = option.Description ?? string.Empty
                })
                .ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var mapping in _catalog.GetRoleMappings())
            {
                var role = await _roleRepository.GetSystemByKeyAsync(mapping.RoleKey, ct);
                if (role is null)
                    continue;

                var existingMappings = await _mappingRepository.GetMappingsForRoleAsync(null, role.Id, ct);
                var existingKeys = existingMappings.Where(x => !x.IsDeleted).Select(x => x.PermissionKey).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var permissionKey in mapping.PermissionKeys.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    if (existingKeys.Contains(permissionKey))
                        continue;

                    var permission = permissions[permissionKey];
                    await _mappingRepository.SaveAsync(new RolePermissionMapping
                    {
                        Id = Guid.NewGuid(),
                        TenantId = null,
                        PartitionKey = RolePermissionMappingRepository.SystemPartitionKey,
                        RowKey = $"ROLE={role.Id:N}|PERM={permission.Id!.Value:N}",
                        RoleId = role.Id,
                        RoleKey = role.Key,
                        PermissionId = permission.Id,
                        PermissionKey = permission.Key,
                        DateCreated = DateTime.UtcNow,
                        DateUpdated = DateTime.UtcNow
                    }, ct);
                }
            }
        }

        private static IReadOnlyList<TenantRoleDefinition> BuildSystemRoles()
        {
            var now = DateTime.UtcNow;
            return TenantRoleCatalog.GetAll().Select(role => new TenantRoleDefinition
            {
                Id = CreateDeterministicGuid($"role:{role.Key}"),
                TenantId = null,
                PartitionKey = TenantRoleDefinitionRepository.SystemPartitionKey,
                RowKey = EntityKeyPolicy.Row(CreateDeterministicGuid($"role:{role.Key}")),
                Key = role.Key,
                Name = role.Name,
                Description = role.Description,
                IsSystem = true,
                IsAssignable = role.IsAssignable,
                GrantsOwnership = role.GrantsOwnership,
                GrantsBillingAdmin = role.GrantsBillingAdmin,
                DateCreated = now,
                DateUpdated = now
            }).ToList();
        }

        private static Guid CreateDeterministicGuid(string value)
        {
            using var md5 = System.Security.Cryptography.MD5.Create();
            return new Guid(md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value)));
        }
    }
}
