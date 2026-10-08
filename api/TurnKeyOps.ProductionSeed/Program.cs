using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using IBeam.Identity.Interfaces;
using IBeam.Identity.Models;
using IBeam.Identity.Repositories.AzureTable.Extensions;
using IBeam.Identity.Repositories.AzureTable.Options;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.API.Configurations;
using MedInsights.Lib;
using MedInsights.Lib.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

if (args.Length < 2) throw new ArgumentException("Usage: ProductionSeed <manifest.json> <api-appsettings.json> [--apply]");
var apply = args.Contains("--apply");
var manifest = JsonSerializer.Deserialize<SeedManifest>(File.ReadAllText(args[0]), new JsonSerializerOptions(JsonSerializerDefaults.Web))
    ?? throw new ArgumentException("Seed manifest is required.");
manifest.Validate();
var connection = Environment.GetEnvironmentVariable("TKO_SEED_STORAGE_CONNECTION")
    ?? throw new InvalidOperationException("Set TKO_SEED_STORAGE_CONNECTION through the authorized deployment environment.");
var tables = new TableServiceClient(connection);
if (tables.Uri.Host != $"{manifest.StorageAccount}.table.core.windows.net")
    throw new InvalidOperationException("The connection does not match the manifest's explicit storage account.");
var config = new ConfigurationBuilder().AddJsonFile(Path.GetFullPath(args[1]))
    .AddInMemoryCollection(new Dictionary<string, string?> {
        ["IBeam:Identity:AzureTable:StorageConnectionString"] = connection,
        ["IBeam:Repositories:AzureTables:ConnectionString"] = connection
    }).Build();
foreach (var tenant in manifest.Tenants)
    if (config[$"UserAdministration:Tenants:{tenant.Key}:TenantId"] != tenant.Id.ToString("D"))
        throw new InvalidOperationException($"Tenant {tenant.Key} does not match deployed application configuration.");
MedInsights.Lib.Utils.RepositoryKeyHelper.ConfigureGuidKeyFormat(config["IBeam:Repositories:AzureTables:GuidKeyFormat"] ?? config["RepositoryKeySettings:GuidFormat"]);
var services = new ServiceCollection();
services.AddLogging();
services.AddMemoryCache();
services.AddSingleton<IConfiguration>(config);
services.AddSingleton<ITenantContext>(new TenantContext());
services.ConfigureIBeamAzureTables(config);
services.AddIBeamAzureTablesRepositories();
services.AddAzureTableMappings();
services.AddIBeamIdentityAzureTable(config);
await using var provider = services.BuildServiceProvider();
var users = provider.GetRequiredService<IIdentityUserStore>();
var roles = provider.GetRequiredService<ITenantRoleStore>();
var memberships = provider.GetRequiredService<ITenantMembershipStore>();
var opts = provider.GetRequiredService<IOptions<AzureTableIdentityOptions>>().Value;
var appMembers = provider.GetRequiredService<IAzureTablesRepositoryStore<TenantMembership>>();
var profiles = provider.GetRequiredService<IAzureTablesRepositoryStore<TenantProfile>>();
var identities = new Dictionary<string, IdentityUser>();
foreach (var person in manifest.Users)
{
    var user = person.Email is not null ? await users.FindByEmailAsync(person.Email) : await users.FindByPhoneAsync(person.Phone!.TrimStart('+'));
    Console.WriteLine($"{person.Key}: {(user is null ? "create unverified identity" : "reuse existing identity")}.");
    if (user is null && apply)
    {
        var created = await users.CreateAsync(new RegisterUserRequest(person.Email, person.Phone?.TrimStart('+'), null!, person.Name));
        if (!created.Succeeded || created.User is null) throw new InvalidOperationException($"Identity creation failed for {person.Key}.");
        user = created.User;
    }
    if (user is not null) identities.Add(person.Key, user);
}
if (!apply)
{
    foreach (var person in manifest.Users)
        foreach (var grant in person.Grants)
            Console.WriteLine($"PLAN {person.Key}: {grant.TenantKey} Owner{(grant.InternalAdmin ? " + platform Internal Admin" : "")}");
    Console.WriteLine("Dry run complete. No user or access records were written.");
    return;
}
if (args.Contains("--repair-seed-keys")) await SeedKeyRepair.RunAsync(tables, manifest.Tenants.Select(t => t.Id).ToArray());
var now = DateTimeOffset.UtcNow;
foreach (var tenant in manifest.Tenants)
{
    var owner = identities[tenant.PrimaryOwnerKey];
    await AddIdentityRow(opts.TenantsTableName, new TableEntity("TEN", opts.TenantsRk(tenant.Id)) {
        ["Name"] = tenant.Name, ["NormalizedName"] = tenant.Name.ToUpperInvariant(),
        ["OwnerUserId"] = owner.UserId.ToString("D"), ["Status"] = "Active", ["CreatedAt"] = now
    }, "Name", tenant.Name);
    await roles.EnsureDefaultRolesAsync(tenant.Id);
    var pk = EntityKeyPolicy.TenantPartition(tenant.Id);
    var profile = await profiles.GetByKeysAsync(pk, EntityKeyPolicy.Row(tenant.Id), default);
    if (profile is null)
        await profiles.AddAsync(tenant.Id, new TenantProfile { Id = tenant.Id, PartitionKey = pk,
            RowKey = EntityKeyPolicy.Row(tenant.Id), TenantName = tenant.Name, Website = tenant.Website,
            IsActive = true, DateCreated = now.UtcDateTime }, default);
}
foreach (var person in manifest.Users)
{
    var user = identities[person.Key];
    foreach (var grant in person.Grants)
    {
        var tenant = manifest.Tenants.Single(t => t.Key == grant.TenantKey);
        var uid = user.UserId.ToString("D");
        var common = new Dictionary<string, object> { ["UserId"] = uid, ["TenantId"] = tenant.Id.ToString("D"),
            ["Status"] = "Active", ["CreatedAt"] = now, ["UserDisplayName"] = person.Name,
            ["RolesCsv"] = "", ["RoleIdsCsv"] = "" };
        var tenantUser = new TableEntity(common) { PartitionKey = opts.TenantUsersPk(tenant.Id), RowKey = opts.TenantUsersRk(uid) };
        if (person.Email is not null) tenantUser["Email"] = person.Email;
        await AddIdentityRow(opts.TenantUsersTableName, tenantUser, "UserId", uid);
        var userTenant = new TableEntity(common) { PartitionKey = opts.UserTenantsPk(uid), RowKey = opts.UserTenantsRk(tenant.Id),
            ["TenantDisplayName"] = tenant.Name, ["IsDefault"] = false };
        await AddIdentityRow(opts.UserTenantsTableName, userTenant, "TenantId", tenant.Id.ToString("D"));
        var needed = grant.InternalAdmin ? new[] { "owner", "internal_admin" } : new[] { "owner" };
        var currentRoles = await roles.GetRolesForUserAsync(tenant.Id, user.UserId);
        var desiredRoleIds = currentRoles.Select(r => r.RoleId).ToHashSet();
        foreach (var name in needed)
        {
            var catalog = await roles.GetRolesAsync(tenant.Id);
            var role = catalog.SingleOrDefault(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase))
                ?? await roles.CreateRoleAsync(tenant.Id, name, false);
            if (!role.IsActive) throw new InvalidOperationException($"Role {name} is inactive.");
            desiredRoleIds.Add(role.RoleId);
        }
        var pk = EntityKeyPolicy.TenantPartition(tenant.Id);
        var found = new List<TenantMembership>();
        await foreach (var m in appMembers.QueryAsync(m => m.PartitionKey == pk && m.UserId == user.UserId, default, "IsDeleted")) found.Add(m);
        if (found.Count > 1) throw new InvalidOperationException("Duplicate application memberships require reconciliation.");
        var existing = found.SingleOrDefault();
        if (existing is not null && (existing.IsDeleted || existing.DateRemoved.HasValue || !string.Equals(existing.MembershipStatus, "Active", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Refusing to reactivate a removed or inactive membership.");
        // The provider filters assignments to the supplied role catalog; pass the full union.
        if (!desiredRoleIds.SetEquals(currentRoles.Select(r => r.RoleId)))
            await roles.GrantRolesAsync(tenant.Id, user.UserId, desiredRoleIds.ToArray());
        if (existing is null)
        {
            var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"owner-seed:{tenant.Id:D}:{uid}"))[..16]);
            await appMembers.AddAsync(tenant.Id, new TenantMembership { Id = id, TenantId = tenant.Id, UserId = user.UserId,
                PartitionKey = pk, RowKey = EntityKeyPolicy.Row(id), Role = "owner", IsOwner = true, IsBillingAdmin = true,
                MembershipStatus = "Active", SeatStatus = "Unassigned", InvitedEmail = person.Email, InvitedPhone = person.Phone,
                DateCreated = now.UtcDateTime }, default);
        }
        else if (existing.Role != "owner" || !existing.IsOwner || !existing.IsBillingAdmin)
        {
            existing.Role = "owner"; existing.IsOwner = true; existing.IsBillingAdmin = true; existing.DateUpdated = now.UtcDateTime;
            await appMembers.UpdateAsync(tenant.Id, existing, TableUpdateMode.Merge, existing.ETag, default);
        }
        var verifiedMemberships = new List<TenantMembership>();
        await foreach (var m in appMembers.QueryAsync(m => m.PartitionKey == pk && m.UserId == user.UserId, default, "IsDeleted")) verifiedMemberships.Add(m);
        if (verifiedMemberships.Count != 1 || verifiedMemberships[0].Role != "owner" || !verifiedMemberships[0].IsOwner)
            throw new InvalidOperationException("Application ownership verification failed.");
        var verifiedRoles = await roles.GetRolesForUserAsync(tenant.Id, user.UserId);
        if (needed.Any(name => !verifiedRoles.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))) throw new InvalidOperationException("Role verification failed.");
        Console.WriteLine($"VERIFIED {person.Key}: {tenant.Key} Owner{(grant.InternalAdmin ? " + platform Internal Admin" : "")}");
    }
    if (args.Contains("--set-default-tenant") || await memberships.GetDefaultTenantIdAsync(user.UserId) is null)
        await memberships.SetDefaultTenantAsync(user.UserId, manifest.Tenants.Single(t => t.Key == person.Grants[0].TenantKey).Id);
}
Console.WriteLine("Seed complete. No passwords, verification flags, or authentication tokens were set.");

async Task AddIdentityRow(string tableName, TableEntity row, string identityColumn, string expected)
{
    var table = tables.GetTableClient(opts.FullTableName(tableName));
    await table.CreateIfNotExistsAsync();
    var current = await table.GetEntityIfExistsAsync<TableEntity>(row.PartitionKey, row.RowKey);
    if (current.HasValue)
    {
        if (current.Value!.GetString(identityColumn) != expected || current.Value.GetString("Status") != "Active")
            throw new InvalidOperationException($"Existing {tableName} record conflicts with the seed or is inactive.");
        return;
    }
    await table.AddEntityAsync(row);
}

record SeedManifest(string StorageAccount, SeedTenant[] Tenants, SeedPerson[] Users)
{
    public void Validate()
    {
        if (StorageAccount != "stturnkeyops") throw new ArgumentException("This bootstrap targets stturnkeyops only.");
        if (Tenants.Length == 0 || Users.Length == 0 || Tenants.Select(t => t.Key).Distinct().Count() != Tenants.Length || Users.Select(u => u.Key).Distinct().Count() != Users.Length)
            throw new ArgumentException("Expected nonempty lists of distinct tenants and users.");
        foreach (var user in Users)
        {
            if ((user.Email is null) == (user.Phone is null) || string.IsNullOrWhiteSpace(user.Name) || user.Grants.Length == 0)
                throw new ArgumentException("Each identity needs a name and exactly one login identifier.");
            if (user.Phone is not null && !System.Text.RegularExpressions.Regex.IsMatch(user.Phone, @"^\+[1-9]\d{7,14}$"))
                throw new ArgumentException("Phone must use E.164 format.");
            foreach (var grant in user.Grants)
                if (!Tenants.Any(t => t.Key == grant.TenantKey)) throw new ArgumentException("Unknown tenant grant.");
        }
        foreach (var tenant in Tenants)
            if (!Users.Any(u => u.Key == tenant.PrimaryOwnerKey && u.Grants.Any(g => g.TenantKey == tenant.Key)))
                throw new ArgumentException("Primary owner must have a grant to the tenant.");
    }
}
record SeedTenant(string Key, Guid Id, string Name, string Website, string PrimaryOwnerKey);
record SeedPerson(string Key, string Name, string? Email, string? Phone, SeedGrant[] Grants);
record SeedGrant(string TenantKey, bool InternalAdmin);
