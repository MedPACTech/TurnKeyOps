using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class TenantRoleService : ITenantRoleService
    {
        public IReadOnlyList<TenantRoleDefinitionDto> GetAll() => TenantRoleCatalog.GetAll();

        public IReadOnlyList<TenantRoleDefinitionDto> GetAssignable() => TenantRoleCatalog.GetAssignable();
    }
}
