namespace MedInsights.Services.Interfaces
{
    public interface IRoleAccessService
    {
        Task RequirePermissionAsync(string permissionKey, CancellationToken ct = default);
        Task RequireAnyRoleAsync(IEnumerable<string> roleKeys, CancellationToken ct = default);
        Task RequireAnyRoleIdAsync(IEnumerable<string> roleIds, CancellationToken ct = default);
        Task<bool> HasPermissionAsync(string permissionKey, CancellationToken ct = default);
        Task<bool> HasAnyRoleAsync(IEnumerable<string> roleKeys, CancellationToken ct = default);
        Task<bool> HasAnyRoleIdAsync(IEnumerable<string> roleIds, CancellationToken ct = default);
        Task EnforceDeclaredAccessAsync(Type targetType, string methodName, CancellationToken ct = default);
    }
}
