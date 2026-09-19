using System.Net.Mail;
using System.Text.RegularExpressions;
using Azure;
using Azure.Data.Tables;
using IBeam.Identity.Interfaces;
using IBeam.Identity.Models;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib;
using MedInsights.Lib.Authorization;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services;

public sealed class ManagedPeopleService(IManagedProfileStore profiles,
    IAzureTablesRepositoryStore<TurnKeyOps.Lib.Entities.Customer> customers,
    ITenantMembershipRepository memberships, IIdentityUserStore identities,
    IUserContext user, UserModuleAccessService access, ITenantMembershipService membershipService,
    IAuditService audit, IInviteService invites, ITenantRoleStore identityRoles)
{
    private string Partition => EntityKeyPolicy.TenantPartition(user.TenantId);
    private async Task RequireAsync(bool write, CancellationToken ct)
    {
        if (!user.IsAuthenticated || user.TenantId == Guid.Empty) throw new UnauthorizedAccessException();
        if (!UserModulePermissions.Allows(await access.GetAsync(ct), "users", write))
            throw new ForbiddenAccessException("User management access is required.");
    }
    public async Task<IReadOnlyList<ManagedPersonDto>> ListAsync(CancellationToken ct)
    {
        await RequireAsync(false, ct);
        var rows = new Dictionary<Guid, UserProfile>();
        var partition = Partition;
        await foreach (var p in profiles.ListAsync(partition, ct))
            rows[p.Id] = p;
        // Existing owners/members appear immediately, without a destructive migration.
        string? continuation = null;
        var memberRows = new List<TenantMembership>();
        do {
            var page = await memberships.GetByPartitionPagedAsync(Partition, 200, continuation, ct);
            memberRows.AddRange(page.Results.Where(m => !m.IsDeleted && m.UserId != Guid.Empty));
            continuation = page.ContinuationToken;
        } while (!string.IsNullOrEmpty(continuation));
        foreach (var m in memberRows.Where(UserModulePermissions.IsActive))
            rows.TryAdd(m.UserId, new UserProfile { Id = m.UserId, ApplicationUserId = m.UserId, IsActive = true,
                ContactEmail = m.InvitedEmail, ContactPhone = m.InvitedPhone, Role = m.Role });
        return rows.Values.Select(p => View(p, memberRows.FirstOrDefault(m => m.UserId == p.Id && UserModulePermissions.IsActive(m))))
            .OrderBy(p => p.FirstName).ThenBy(p => p.LastName).ToArray();
    }
    public async Task<ManagedPersonDto> SaveAsync(Guid? id, SaveManagedPersonDto input, CancellationToken ct)
    {
        await RequireAsync(true, ct);
        Validate(input);
        if (input.CustomerId.HasValue && await customers.GetByKeysAsync(Partition, EntityKeyPolicy.Row(input.CustomerId.Value), ct) is not { IsDeleted: false })
            throw new ArgumentException("The linked customer must belong to this company.");
        UserProfile? existing = null;
        string? loginEmail = null, loginPhone = null;
        if (id.HasValue) {
            existing = await profiles.GetByKeysAsync(Partition, EntityKeyPolicy.Row(id.Value), ct);
            if (existing is null && await memberships.GetByUserIdAsync(Partition, id.Value, ct) is null)
                throw new KeyNotFoundException("User not found in this company.");
        } else {
            var login = input.LoginIdentifier?.Trim() ?? "";
            if (login.Contains('@') && (!MailAddress.TryCreate(login, out var address) || !string.Equals(login, address.Address, StringComparison.OrdinalIgnoreCase)))
                throw new ArgumentException("Enter a valid login email.");
            var email = login.Contains('@') ? login.ToLowerInvariant() : null;
            var phone = email is null ? Regex.Replace(login, "[ ()-]", "") : null;
            if (email is null && !Regex.IsMatch(phone ?? "", @"^\+[1-9]\d{7,14}$"))
                throw new ArgumentException("Enter an email or international phone number (+country code).");
            var identity = email is not null ? await identities.FindByEmailAsync(email) : await identities.FindByPhoneAsync(phone!.TrimStart('+'));
            if (identity is null) {
                var created = await identities.CreateAsync(new RegisterUserRequest(email, phone?.TrimStart('+'), null!, $"{input.FirstName} {input.LastName}".Trim()));
                if (!created.Succeeded || created.User is null) throw new ArgumentException("Could not create the user identity.");
                identity = created.User;
            }
            id = identity.UserId;
            loginEmail = email; loginPhone = phone;
            existing = await profiles.GetByKeysAsync(Partition, EntityKeyPolicy.Row(id.Value), ct);
            if (existing is not null || await memberships.GetByUserIdAsync(Partition, id.Value, ct) is not null)
                throw new ArgumentException("This user already exists in this company. Edit the existing record.");
        }
        if (existing is not null && (existing.IsDeleted || !existing.IsActive))
            throw new ArgumentException("This user is archived. Restore the record before editing.");
        if (existing is not null && existing.ETag.ToString() != input.ExpectedVersion)
            throw new ArgumentException("This record changed. Reload before saving.");
        var membership = await memberships.GetByUserIdAsync(Partition, id!.Value, ct);
        if (UserModulePermissions.IsActive(membership) && membership!.Role is "owner" or "admin" or "staff" or "member" && !input.ProfileTypes.Contains("employee"))
            throw new ArgumentException("Users with employee access roles must retain their employee profile.");
        var owner = membership?.IsOwner == true || membership?.Role == "owner";
        if (owner && input.ModulePermissions is not null) throw new ArgumentException("Owners retain full module access.");
        if (id == user.UserId && input.ModulePermissions is not null) throw new ArgumentException("You cannot restrict your own access.");
        var currentPermissions = await access.GetAsync(ct);
        var actor = await memberships.GetByUserIdAsync(Partition, user.UserId, ct);
        if (actor?.IsOwner != true && actor?.Role != "owner") {
            if (owner) throw new ForbiddenAccessException("Only an owner can edit another owner.");
            if (input.ModulePermissions is null || input.ModulePermissions.Except(currentPermissions).Any())
                throw new ForbiddenAccessException("Choose explicit permissions within your own access.");
        }
        var p = existing ?? new UserProfile { Id = id.Value, ApplicationUserId = id.Value, PartitionKey = Partition,
            RowKey = EntityKeyPolicy.Row(id.Value), PrimaryEmail = loginEmail ?? membership?.InvitedEmail, PrimaryPhone = loginPhone ?? membership?.InvitedPhone, IsActive = true, Role = membership?.Role ?? "contact" };
        p.FirstName = input.FirstName.Trim(); p.LastName = input.LastName.Trim();
        p.ContactEmail = input.ContactEmail?.Trim(); p.ContactPhone = input.ContactPhone?.Trim();
        p.ProfileTypes = input.ProfileTypes.Distinct().ToArray(); p.CompanyName = input.CompanyName?.Trim();
        p.Title = input.Title?.Trim(); p.Team = input.Team?.Trim(); p.CustomerId = input.CustomerId;
        p.ModulePermissions = UserModulePermissions.Validate(input.ModulePermissions);
        if (existing is null) await profiles.AddAsync(user.TenantId, p, ct);
        else await profiles.UpdateAsync(user.TenantId, p, TableUpdateMode.Replace, existing.ETag, ct);
        await AuditAsync(p.Id, "user_profile_saved", ct);
        var saved = await profiles.GetByKeysAsync(Partition, p.RowKey, ct) ?? p;
        return View(saved, membership);
    }
    public async Task ArchiveAsync(Guid id, string version, CancellationToken ct)
    {
        await RequireAsync(true, ct);
        if (id == user.UserId) throw new ArgumentException("You cannot archive yourself.");
        var m = await memberships.GetByUserIdAsync(Partition, id, ct);
        if (m?.IsOwner == true || m?.Role == "owner") throw new ArgumentException("Owner accounts cannot be archived.");
        var p = await profiles.GetByKeysAsync(Partition, EntityKeyPolicy.Row(id), ct);
        if (p is null && m is null) throw new KeyNotFoundException("User not found in this company.");
        if (p is not null && p.ETag.ToString() != version) throw new ArgumentException("This record changed. Reload before archiving.");
        if (m is not null && UserModulePermissions.IsActive(m)) await membershipService.RemoveAsync(m.Id, ct);
        if (p is not null) {
            p.IsActive = false; p.IsDeleted = true;
            await profiles.UpdateAsync(user.TenantId, p, TableUpdateMode.Replace, p.ETag, ct);
        }
        await AuditAsync(id, "user_archived", ct);
    }
    public async Task<IReadOnlyList<object>> CustomersAsync(CancellationToken ct)
    {
        await RequireAsync(false, ct);
        var partition = Partition;
        var result = new List<object>();
        await foreach (var c in customers.QueryAsync(c => c.PartitionKey == partition, ct, "PartitionKey"))
            if (!c.IsDeleted) result.Add(new { c.Id, Name = $"{c.FirstName} {c.LastName}".Trim(), c.CompanyName });
        return result;
    }
    public async Task UpdateRoleAsync(Guid id, string role, CancellationToken ct)
    {
        await RequireAsync(true, ct);
        if (!await access.IsOwnerAsync(ct)) throw new ForbiddenAccessException("Only an owner can change access roles.");
        var p = await profiles.GetByKeysAsync(Partition, EntityKeyPolicy.Row(id), ct) ?? throw new KeyNotFoundException("Save a profile for this user first.");
        if (!p.IsActive || p.IsDeleted) throw new ArgumentException("User is archived.");
        if (role is not ("owner" or "admin" or "staff" or "member" or "contact")) throw new ArgumentException("Choose an assignable role.");
        if (role != "contact" && !p.ProfileTypes.Contains("employee")) throw new ArgumentException("Administrative access requires an employee profile.");
        var m = await memberships.GetByUserIdAsync(Partition, id, ct) ?? throw new KeyNotFoundException("Invite this user first.");
        if (!UserModulePermissions.IsActive(m)) throw new ArgumentException("Invite this user to restore company access.");
        if (m.IsOwner || m.Role == "owner") throw new ArgumentException("Existing owners retain ownership.");
        if (role == "owner") {
            var catalog = await identityRoles.GetRolesAsync(user.TenantId);
            var ownerRole = catalog.SingleOrDefault(r => string.Equals(r.Name, "owner", StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException("The company owner role is not configured.");
            if (!ownerRole.IsActive) throw new ArgumentException("The company owner role is inactive.");
            var assigned = await identityRoles.GetRolesForUserAsync(user.TenantId, id);
            await identityRoles.GrantRolesAsync(user.TenantId, id, assigned.Select(r => r.RoleId).Append(ownerRole.RoleId).Distinct().ToArray());
            m.Role = "owner"; m.IsOwner = true; m.IsBillingAdmin = true; m.DateUpdated = DateTime.UtcNow;
            await memberships.SaveAsync(m, ct);
            await AuditAsync(id, "user_granted_owner", ct);
        } else await membershipService.UpdateRoleAsync(m.Id, new() { Role = role }, ct);
    }
    public async Task<InviteDto> InviteAsync(Guid id, string role, CancellationToken ct)
    {
        await RequireAsync(true, ct);
        var p = await profiles.GetByKeysAsync(Partition, EntityKeyPolicy.Row(id), ct)
            ?? throw new KeyNotFoundException("User not found.");
        if (p.IsDeleted || !p.IsActive) throw new ArgumentException("Restore this user before inviting them.");
        if (role is not ("admin" or "staff" or "member" or "contact")) throw new ArgumentException("Choose an assignable role.");
        if (role != "contact" && !p.ProfileTypes.Contains("employee")) throw new ArgumentException("Administrative access requires an employee profile.");
        var actor = await memberships.GetByUserIdAsync(Partition, user.UserId, ct);
        if (role == "admin" && actor?.IsOwner != true && actor?.Role != "owner") throw new ForbiddenAccessException("Only owners can invite administrators.");
        if (UserModulePermissions.IsActive(await memberships.GetByUserIdAsync(Partition, id, ct)))
            throw new ArgumentException("This user already has access. Edit their role instead.");
        return await invites.CreateAsync(new() { InvitedEmail = p.PrimaryEmail, InvitedPhone = p.PrimaryPhone, Role = role }, ct);
    }
    public async Task RestoreAsync(Guid id, string version, CancellationToken ct)
    {
        await RequireAsync(true, ct);
        var p = await profiles.GetByKeysAsync(Partition, EntityKeyPolicy.Row(id), ct) ?? throw new KeyNotFoundException("User not found.");
        if (p.ETag.ToString() != version) throw new ArgumentException("This record changed. Reload before restoring.");
        p.IsDeleted = false; p.IsActive = true;
        await profiles.UpdateAsync(user.TenantId, p, TableUpdateMode.Replace, p.ETag, ct);
        await AuditAsync(id, "user_restored_without_access", ct);
    }
    private Task<AuditEventDto> AuditAsync(Guid id, string action, CancellationToken ct) => audit.RecordAsync(new() {
        Category = "admin", Action = action, TargetType = "user", TargetId = id.ToString(), Source = nameof(ManagedPeopleService)
    }, ct);
    public static void Validate(SaveManagedPersonDto input)
    {
        if (string.IsNullOrWhiteSpace(input.FirstName) || input.FirstName.Length > 100 || (input.LastName is null || input.LastName.Length > 100))
            throw new ArgumentException("Enter a name of at most 100 characters per field.");
        if (input.ProfileTypes is null || input.ProfileTypes.Length > 3 || input.ProfileTypes.Any(t => t is not ("employee" or "customer" or "vendor")))
            throw new ArgumentException("Unknown business profile.");
        if (!string.IsNullOrWhiteSpace(input.ContactEmail) && !MailAddress.TryCreate(input.ContactEmail, out _))
            throw new ArgumentException("Enter a valid contact email.");
        if (new[] { input.ContactPhone, input.CompanyName, input.Title, input.Team }.Any(s => s?.Length > 200))
            throw new ArgumentException("Contact and business details must be at most 200 characters.");
        UserModulePermissions.Validate(input.ModulePermissions);
    }
    private static ManagedPersonDto View(UserProfile p, TenantMembership? m) => new(p.Id, p.FirstName, p.LastName,
        p.ContactEmail ?? p.PrimaryEmail ?? m?.InvitedEmail, p.ContactPhone ?? p.PrimaryPhone ?? m?.InvitedPhone,
        p.ProfileTypes.Length == 0 && m?.Role is "owner" or "admin" or "staff" or "member" ? ["employee"] : p.ProfileTypes,
        p.CompanyName, p.Title, p.Team, p.CustomerId, p.ModulePermissions,
        UserModulePermissions.Resolve(m,p), m?.Role, m?.Id, m?.IsOwner == true || m?.Role == "owner", p.IsActive, p.ETag.ToString());
}
