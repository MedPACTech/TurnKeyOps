using System.Text.Json;
using System.Runtime.CompilerServices;
using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Configuration;
namespace MedInsights.Services;

// IBeam 2.0.32's envelope reader restores payload fields but loses the physical ETag.
// Read the actual table metadata; never replace concurrency checks with wildcard writes.
public sealed class ManagedProfileStore : IManagedProfileStore
{
    private readonly IAzureTablesRepositoryStore<UserProfile> _store;
    private readonly TableClient _table;
    public ManagedProfileStore(IAzureTablesRepositoryStore<UserProfile> store, IConfiguration configuration)
    {
        _store=store;
        var connection=configuration["IBeam:Repositories:AzureTables:ConnectionString"]
            ?? throw new InvalidOperationException("Profile storage is not configured.");
        _table=new TableServiceClient(connection).GetTableClient((configuration["IBeam:Repositories:AzureTables:TableNamePrefix"] ?? "")+"UserProfiles");
    }
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    // Decode the envelope and concurrency metadata from the SAME response. Separate
    // payload/metadata reads could pair an old value with a newer ETag and lose edits.
    private static UserProfile Read(TableEntity row)
    {
        var profile = JsonSerializer.Deserialize<UserProfile>(row.GetString("Data"), JsonOptions)
            ?? throw new InvalidOperationException("Invalid user profile envelope.");
        profile.PartitionKey = row.PartitionKey;
        profile.RowKey = row.RowKey;
        profile.ETag = row.ETag;
        profile.Timestamp = row.Timestamp;
        return profile;
    }
    public async Task<UserProfile?> GetByKeysAsync(string partition, string row, CancellationToken ct)
    {
        try {
            var result = await _table.GetEntityIfExistsAsync<TableEntity>(partition, row, cancellationToken: ct);
            return result.HasValue ? Read(result.Value) : null;
        } catch (RequestFailedException e) when (e.Status == 404) { return null; }
    }
    public async IAsyncEnumerable<UserProfile> ListAsync(string partition,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Include archived profiles so that owners can restore them. The generic
        // repository's QueryAsync always filters soft-deleted records.
        await foreach (var row in _table.QueryAsync<TableEntity>(
            TableClient.CreateQueryFilter($"PartitionKey eq {partition}"), cancellationToken: ct))
            yield return Read(row);
    }
    public Task<UserProfile> AddAsync(Guid? tenant,UserProfile profile,CancellationToken ct) => _store.AddAsync(tenant,profile,ct);
    public Task<UserProfile> UpdateAsync(Guid? tenant,UserProfile profile,TableUpdateMode mode,ETag? version,CancellationToken ct)
    {
        if (version is null || string.IsNullOrWhiteSpace(version.ToString()) || version.Value.Equals(ETag.All))
            throw new ArgumentException("A current record version is required.");
        return _store.UpdateAsync(tenant,profile,mode,version,ct);
    }
}
