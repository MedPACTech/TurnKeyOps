using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;

namespace MedInsights.Services;

public sealed class UserModuleAccessService(ITenantMembershipRepository memberships,
    IAzureTablesRepositoryStore<UserProfile> profiles, IUserContext user)
{
    private Task<string[]>? _permissions;
    private bool _isOwner;
    public async Task<bool> IsOwnerAsync(CancellationToken ct = default) { await GetAsync(ct); return _isOwner; }
    public Task<string[]> GetAsync(CancellationToken ct = default) => _permissions ??= LoadAsync(ct);
    private async Task<string[]> LoadAsync(CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.TenantId == Guid.Empty) return [];
        var pk = EntityKeyPolicy.TenantPartition(user.TenantId);
        var membership = await memberships.GetByUserIdAsync(pk, user.UserId, ct);
        _isOwner = UserModulePermissions.IsActive(membership) && (membership!.IsOwner || membership.Role == "owner");
        var profile = await profiles.GetByKeysAsync(pk, EntityKeyPolicy.Row(user.UserId), ct);
        return UserModulePermissions.Resolve(membership, profile);
    }
}
