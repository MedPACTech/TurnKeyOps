using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ITenantRoleService
    {
        IReadOnlyList<TenantRoleDefinitionDto> GetAll();
        IReadOnlyList<TenantRoleDefinitionDto> GetAssignable();
    }
}
