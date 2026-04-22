using MedInsights.Lib.Authorization;
using MedInsights.Lib.Configurations;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.Services
{
    public sealed class RolePermissionCatalog : IRolePermissionCatalog
    {
        private readonly AuthorizationOptions _options;
        private readonly PermissionRegistrationBuilder _builder;

        public RolePermissionCatalog(
            IOptions<AuthorizationOptions> options,
            PermissionRegistrationBuilder builder)
        {
            _options = options.Value;
            _builder = builder;
        }

        public IReadOnlyList<PermissionDefinitionOption> GetPermissions()
            => _options.Permissions
                .Concat(_builder.Permissions)
                .GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.Last())
                .OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

        public IReadOnlyList<RolePermissionSeedOption> GetRoleMappings()
            => _options.RoleMappings
                .Concat(_builder.RoleMappings)
                .GroupBy(x => x.RoleKey, StringComparer.OrdinalIgnoreCase)
                .Select(group =>
                {
                    var merged = new RolePermissionSeedOption
                    {
                        RoleKey = group.Last().RoleKey,
                        RoleId = group.Last().RoleId
                    };

                    merged.PermissionKeys = group
                        .SelectMany(x => x.PermissionKeys)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    merged.PermissionIds = group
                        .SelectMany(x => x.PermissionIds)
                        .Distinct()
                        .ToList();

                    return merged;
                })
                .OrderBy(x => x.RoleKey, StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
