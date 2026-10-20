using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;

namespace MedInsights.Services;

public sealed class UserModuleAccessService(ITenantMembershipRepository memberships,
    IAzureTablesRepositoryStore<UserProfile> profiles, IUserContext user, TurnKeyOps.Repositories.Interfaces.ITenantSettingsRepository? settings = null)
{
    private Task<string[]>? _permissions;
    private bool _isOwner;
    private bool _hasEmployeeRole;
    public async Task<bool> HasEmployeeRoleAsync(CancellationToken ct = default) { await GetAsync(ct); return _hasEmployeeRole; }
    public async Task<bool> IsOwnerAsync(CancellationToken ct = default) { await GetAsync(ct); return _isOwner; }
    public Task<string[]> GetAsync(CancellationToken ct = default) => _permissions ??= LoadAsync(ct);
    public async Task<EmployeeAccessConfiguration> ConfigurationAsync(CancellationToken ct = default)
    {
        if (!user.IsAuthenticated || user.TenantId == Guid.Empty) throw new UnauthorizedAccessException();
        var document = settings is null ? null : await settings.GetAsync(EntityKeyPolicy.TenantPartition(user.TenantId), "SETTINGS|EMPLOYEE-ACCESS", ct);
        return EmployeeAccessConfiguration.Parse(document is { IsDeleted: false } ? document.ValuesJson : null);
    }
    private async Task<string[]> LoadAsync(CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.TenantId == Guid.Empty) return [];
        var pk = EntityKeyPolicy.TenantPartition(user.TenantId);
        var membership = await memberships.GetByUserIdAsync(pk, user.UserId, ct);
        _isOwner = UserModulePermissions.IsActive(membership) && (membership!.IsOwner || membership.Role == "owner");
        var profile = await profiles.GetByKeysAsync(pk, EntityKeyPolicy.Row(user.UserId), ct);
        var configuration = await ConfigurationAsync(ct);
        _hasEmployeeRole = UserModulePermissions.IsActive(membership) && profile is { IsActive: true, IsDeleted: false } && configuration.Roles.Any(r => UserModulePermissions.RoleIds(profile).Contains(r.Id));
        return UserModulePermissions.Resolve(membership, profile, configuration);
    }
}
