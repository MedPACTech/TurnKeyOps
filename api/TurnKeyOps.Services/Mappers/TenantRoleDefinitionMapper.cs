using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class TenantRoleDefinitionMapper
    {
        public static TenantRoleDto ToDto(
            TenantRoleDefinition entity,
            IEnumerable<PermissionDefinitionDto> permissions)
            => new()
            {
                Id = entity.Id,
                TenantId = entity.TenantId,
                Key = entity.Key,
                Name = entity.Name,
                Description = entity.Description,
                IsSystem = entity.IsSystem,
                IsAssignable = entity.IsAssignable,
                GrantsOwnership = entity.GrantsOwnership,
                GrantsBillingAdmin = entity.GrantsBillingAdmin,
                Permissions = permissions.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase).ToList(),
                DateCreated = entity.DateCreated,
                DateUpdated = entity.DateUpdated
            };
    }
}
