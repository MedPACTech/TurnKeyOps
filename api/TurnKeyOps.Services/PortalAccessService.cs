using System.Security.Cryptography;
using System.Text;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Lib.Dtos;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;

namespace TurnKeyOps.Services;

public sealed class PortalAccessService(IPortalAccessStore store, IManagedProfileStore profiles, IAuditService audit)
{
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static bool ProfileAllows(UserProfile? profile, Guid tenant, Guid user, Guid customer) =>
        profile is { IsDeleted: false, IsActive: true } && profile.PartitionKey == RepositoryKeyHelper.ToTenantPartitionKey(tenant) &&
        profile.ApplicationUserId == user && profile.CustomerId == customer &&
        profile.ProfileTypes.Any(p => p.Equals("Customer", StringComparison.OrdinalIgnoreCase));

    private async Task<List<PortalGrant>> Active(Guid tenant, Guid user, PortalAccessState state, CancellationToken ct)
    {
        var profile = await profiles.GetByKeysAsync(RepositoryKeyHelper.ToTenantPartitionKey(tenant), RepositoryKeyHelper.ToRowKey(user), ct);
        return state.Grants.Where(g => g.UserId == user && !g.Revoked && g.ExpiresAtUtc > DateTime.UtcNow &&
            ProfileAllows(profile, tenant, user, g.CustomerId)).ToList();
    }
    // This is called only with the subject from an API-validated iBeam identity token.
    public async Task<object> ActivateAsync(Guid tenant, Guid user, CancellationToken ct)
    {
        if (user == Guid.Empty || tenant == Guid.Empty) throw new UnauthorizedAccessException();
        var state = await store.ReadAsync(tenant, ct);
        if (!state.Configuration.Enabled || (await Active(tenant, user, state, ct)).Count == 0) throw new UnauthorizedAccessException();
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        state.Sessions.RemoveAll(s => s.ExpiresAtUtc <= DateTime.UtcNow || s.Revoked);
        // Bound session growth without affecting other identities.
        foreach (var old in state.Sessions.Where(s => s.UserId == user).OrderByDescending(s => s.ExpiresAtUtc).Skip(4)) old.Revoked = true;
        var expires = DateTime.UtcNow.AddHours(8);
        state.Sessions.Add(new() { TokenHash = Hash(token), UserId = user, ExpiresAtUtc = expires });
        await store.SaveAsync(tenant, state, state.Version, ct);
        await Audit(tenant, user, "portal.activation", null, ct);
        return new { token, expiresAtUtc = expires };
    }
    public async Task<PortalActor> AuthenticateAsync(Guid tenant, string? token, CancellationToken ct)
    {
        if (tenant == Guid.Empty || token is null || token.Length != 64) throw new UnauthorizedAccessException();
        var state = await store.ReadAsync(tenant, ct);
        var session = state.Sessions.SingleOrDefault(s => s.TokenHash == Hash(token) && !s.Revoked && s.ExpiresAtUtc > DateTime.UtcNow);
        if (!state.Configuration.Enabled || session is null) throw new UnauthorizedAccessException();
        var grants = await Active(tenant, session.UserId, state, ct);
        if (grants.Count == 0) throw new UnauthorizedAccessException();
        return new(tenant, session.UserId, grants, state.Configuration);
    }
    public async Task LogoutAsync(Guid tenant, string token, CancellationToken ct)
    {
        var state = await store.ReadAsync(tenant, ct);
        var session = state.Sessions.SingleOrDefault(s => s.TokenHash == Hash(token));
        if (session is null) return;
        session.Revoked = true;
        await store.SaveAsync(tenant, state, state.Version, ct);
    }
    // Owner authorization is enforced by PortalAdminController before this operation.
    public async Task RevokeContactAsync(Guid tenant, Guid operatorId, Guid contact, string expectedVersion, CancellationToken ct)
    {
        var state = await store.ReadAsync(tenant, ct);
        PortalRules.ExactVersion(expectedVersion, state.Version);
        foreach (var grant in state.Grants.Where(g => g.UserId == contact)) grant.Revoked = true;
        foreach (var session in state.Sessions.Where(s => s.UserId == contact)) session.Revoked = true;
        await store.SaveAsync(tenant, state, state.Version, ct);
        await Audit(tenant, operatorId, "contact.access-revoked", contact, ct);
    }
    public static bool Allows(PortalActor actor, string kind, Guid id, Guid? customer, Guid? site = null) => customer.HasValue &&
        actor.Grants.Any(g => !g.Revoked && g.ExpiresAtUtc > DateTime.UtcNow && g.UserId == actor.UserId && g.CustomerId == customer &&
            (g.Scope == "customer" && g.RecordId == customer || g.Scope == "site" && site.HasValue && g.RecordId == site || g.Scope == kind && g.RecordId == id));
    public async Task<PortalActor> NotificationActorAsync(Guid tenant, Guid user, CancellationToken ct)
    {
        var state=await store.ReadAsync(tenant,ct);var grants=await Active(tenant,user,state,ct);
        if(!state.Configuration.Enabled||grants.Count==0)throw new KeyNotFoundException();
        return new(tenant,user,grants,state.Configuration);
    }
    public async Task<List<string>> PreferencesAsync(PortalActor a,CancellationToken ct) => (await store.ReadAsync(a.TenantId,ct)).NotificationPreferences.GetValueOrDefault(a.UserId)??[];
    public async Task SavePreferencesAsync(PortalActor a,List<string> channels,CancellationToken ct)
    {
        if(channels.Any(c=>c is not ("email" or "sms")))throw new ArgumentException("Choose email and/or SMS.");
        var state=await store.ReadAsync(a.TenantId,ct);state.NotificationPreferences[a.UserId]=channels.Distinct().ToList();
        await store.SaveAsync(a.TenantId,state,state.Version,ct);await Audit(a.TenantId,a.UserId,"preferences.updated",null,ct);
    }
    public Task Audit(Guid tenant, Guid user, string action, Guid? target, CancellationToken ct) => audit.RecordAsync(new RecordAuditEventRequestDto {
        TenantId = tenant, UserId = user, Category = "customer-portal", Action = action, TargetType = "portal-record", TargetId = target?.ToString("D"),
        Source = "customer-portal", Description = "Customer portal action", MetadataJson = "{}"
    }, ct);
}
