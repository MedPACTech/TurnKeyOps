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
    private async Task RequireAsync(bool write, CancellationToken ct, string module = "users")
    {
        if (!user.IsAuthenticated || user.TenantId == Guid.Empty) throw new UnauthorizedAccessException();
        if (!UserModulePermissions.Allows(await access.GetAsync(ct), module, write))
            throw new ForbiddenAccessException("Access to this module is required.");
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
    public Task<ManagedPersonDto> SaveAsync(Guid? id, SaveManagedPersonDto input, CancellationToken ct)
        => SaveCoreAsync(id, input, ct);
    private async Task<ManagedPersonDto> SaveCoreAsync(Guid? id, SaveManagedPersonDto input, CancellationToken ct, SaveContactRecordDto? contact = null)
    {
        await RequireAsync(true, ct, contact is null ? "users" : "contacts");
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
        if (contact is null && UserModulePermissions.IsActive(membership) && membership!.Role is "owner" or "admin" or "staff" or "member" && !input.ProfileTypes.Contains("employee"))
            throw new ArgumentException("Users with employee access roles must retain their employee profile.");
        var owner = membership?.IsOwner == true || membership?.Role == "owner";
        if (contact is null && owner && input.ModulePermissions is not null) throw new ArgumentException("Owners retain full module access.");
        if (contact is null && id == user.UserId && input.ModulePermissions is not null) throw new ArgumentException("You cannot restrict your own access.");
        var currentPermissions = await access.GetAsync(ct);
        var actor = await memberships.GetByUserIdAsync(Partition, user.UserId, ct);
        if (contact is null && actor?.IsOwner != true && actor?.Role != "owner") {
            if (owner) throw new ForbiddenAccessException("Only an owner can edit another owner.");
            if (input.ModulePermissions is null || input.ModulePermissions.Except(currentPermissions).Any())
                throw new ForbiddenAccessException("Choose explicit permissions within your own access.");
        }
        var p = existing ?? new UserProfile { Id = id.Value, ApplicationUserId = id.Value, PartitionKey = Partition,
            RowKey = EntityKeyPolicy.Row(id.Value), PrimaryEmail = loginEmail ?? membership?.InvitedEmail, PrimaryPhone = loginPhone ?? membership?.InvitedPhone, IsActive = true, Role = membership?.Role ?? "contact" };
        p.FirstName = input.FirstName.Trim(); p.LastName = input.LastName.Trim();
        p.ContactEmail = input.ContactEmail?.Trim(); p.ContactPhone = input.ContactPhone?.Trim();
        p.ProfileTypes = contact is null ? input.ProfileTypes.Distinct().ToArray()
            : p.ProfileTypes.Where(t => t == "employee").Concat(input.ProfileTypes).Distinct().ToArray();
        p.CompanyName = input.CompanyName?.Trim(); p.CustomerId = input.CustomerId;
        if (contact is null) {
            p.Title = input.Title?.Trim(); p.Team = input.Team?.Trim();
            p.ModulePermissions = UserModulePermissions.Validate(input.ModulePermissions);
        } else {
            p.AddressLine1 = contact.Address?.Trim(); p.City = contact.City?.Trim();
            p.State = contact.State?.Trim(); p.PostalCode = contact.PostalCode?.Trim(); p.ContactNotes = contact.Notes?.Trim();
        }
        if (existing is null) await profiles.AddAsync(user.TenantId, p, ct);
        else await profiles.UpdateAsync(user.TenantId, p, TableUpdateMode.Replace, existing.ETag, ct);
        await AuditAsync(p.Id, contact is null ? "user_profile_saved" : "contact_saved", ct);
        var saved = await profiles.GetByKeysAsync(Partition, p.RowKey, ct) ?? p;
        return View(saved, membership);
    }
    public async Task DeleteAsync(Guid id, string version, CancellationToken ct)
    {
        await RequireAsync(true, ct);
        if (!await access.IsOwnerAsync(ct)) throw new ForbiddenAccessException("Only an owner can delete a user.");
        await ArchiveCoreAsync(id, version, true, ct);
    }
    public Task ArchiveAsync(Guid id, string version, CancellationToken ct) => ArchiveCoreAsync(id, version, false, ct);
    private async Task ArchiveCoreAsync(Guid id, string version, bool ownerRemoval, CancellationToken ct)
    {
        await RequireAsync(true, ct);
        if (id == user.UserId) throw new ArgumentException("You cannot delete your own company access. Ask another owner to remove you.");
        var m = await memberships.GetByUserIdAsync(Partition, id, ct);
        if (!ownerRemoval && (m?.IsOwner == true || m?.Role == "owner")) throw new ArgumentException("Use the owner-only Delete user action to remove another owner.");
        var p = await profiles.GetByKeysAsync(Partition, EntityKeyPolicy.Row(id), ct);
        if (p is null && m is null) throw new KeyNotFoundException("User not found in this company.");
        if (p is not null && p.ETag.ToString() != version) throw new ArgumentException("This record changed. Reload before removing the user.");
        if (m is not null && UserModulePermissions.IsActive(m)) await membershipService.RemoveAsync(m.Id, ct);
        if (p is not null) {
            p.IsActive = false; p.IsDeleted = true;
            await profiles.UpdateAsync(user.TenantId, p, TableUpdateMode.Replace, p.ETag, ct);
        }
        await AuditAsync(id, ownerRemoval ? "user_deleted_from_company" : "user_archived", ct);
    }
    public async Task<IReadOnlyList<object>> CustomersAsync(CancellationToken ct, bool forContacts = false)
    {
        await RequireAsync(false, ct, forContacts ? "contacts" : "users");
        var partition = Partition;
        var result = new List<object>();
        await foreach (var c in customers.QueryAsync(c => c.PartitionKey == partition, ct, "PartitionKey"))
            if (!c.IsDeleted) result.Add(new { c.Id, Name = $"{c.FirstName} {c.LastName}".Trim(), c.CompanyName });
        return result;
    }
    public async Task<IReadOnlyList<ContactRecordDto>> ContactsAsync(CancellationToken ct)
    {
        await RequireAsync(false, ct, "contacts");
        var result = new List<ContactRecordDto>();
        await foreach (var p in profiles.ListAsync(Partition, ct))
            if (p.IsActive && !p.IsDeleted && p.ProfileTypes.Any(t => t is "customer" or "vendor")) result.Add(ContactView(p));
        return result.OrderBy(p => p.FirstName).ThenBy(p => p.LastName).ToArray();
    }
    public async Task<ContactRecordDto> ContactAsync(Guid id, CancellationToken ct)
    {
        await RequireAsync(false, ct, "contacts");
        var p = await profiles.GetByKeysAsync(Partition, EntityKeyPolicy.Row(id), ct);
        if (p is null || !p.IsActive || p.IsDeleted || !p.ProfileTypes.Any(t => t is "customer" or "vendor"))
            throw new KeyNotFoundException("Contact not found in this company.");
        return ContactView(p);
    }
    public async Task<ContactRecordDto> SaveContactAsync(Guid? id, SaveContactRecordDto input, CancellationToken ct)
    {
        await RequireAsync(true, ct, "contacts");
        if (input.ProfileTypes is null || input.ProfileTypes.Length == 0 || input.ProfileTypes.Any(t => t is not ("customer" or "vendor")))
            throw new ArgumentException("Choose Customer, Vendor, or both.");
        if (new[] {input.Address, input.City, input.State, input.PostalCode}.Any(s => s?.Length > 200) || input.Notes?.Length > 4000)
            throw new ArgumentException("Address fields must be at most 200 characters and notes at most 4000.");
        if (id.HasValue) await ContactAsync(id.Value, ct);
        var saved = await SaveCoreAsync(id, new() {
            FirstName = input.FirstName, LastName = input.LastName, ContactEmail = input.ContactEmail,
            ContactPhone = input.ContactPhone, CompanyName = input.CompanyName, ProfileTypes = input.ProfileTypes,
            CustomerId = input.CustomerId, ExpectedVersion = input.ExpectedVersion,
            LoginIdentifier = string.IsNullOrWhiteSpace(input.ContactEmail) ? input.ContactPhone : input.ContactEmail
        }, ct, input);
        return await ContactAsync(saved.Id, ct);
    }
    private static ContactRecordDto ContactView(UserProfile p) => new(p.Id, p.FirstName, p.LastName,
        p.ContactEmail ?? p.PrimaryEmail, p.ContactPhone ?? p.PrimaryPhone, p.CompanyName,
        p.ProfileTypes.Where(t => t is "customer" or "vendor").ToArray(), p.CustomerId,
        p.AddressLine1, p.City, p.State, p.PostalCode, p.ContactNotes, p.ETag.ToString());
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
