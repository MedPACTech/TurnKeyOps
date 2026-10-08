using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace TurnKeyOps.Repositories;

// IBeam 2.0.32 omits physical ETags when decoding JSON envelopes. Keep the
// existing table/JSON format, but read data and metadata atomically and use
// the write response's ETag (never a second read or wildcard update).
public sealed class VersionedEnvelopeStore<T> where T : class, ITableEntity
{
    private readonly TableClient _table;
    private readonly bool _createTables;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public VersionedEnvelopeStore(IConfiguration configuration,string tableName)
    {
        var connection=configuration["IBeam:Repositories:AzureTables:ConnectionString"]??throw new InvalidOperationException("Table storage is not configured.");
        _table=new TableServiceClient(connection).GetTableClient((configuration["IBeam:Repositories:AzureTables:TableNamePrefix"]??"")+tableName);
        _createTables=configuration["IBeam:Repositories:AzureTables:CreateTablesIfNotExists"]!="false";
    }
    private static T Read(TableEntity row)
    {
        var entity=JsonSerializer.Deserialize<T>(row.GetString("Data")??throw new InvalidOperationException("Missing envelope data."),Json)??throw new InvalidOperationException("Invalid envelope data.");
        entity.PartitionKey=row.PartitionKey;entity.RowKey=row.RowKey;entity.ETag=row.ETag;entity.Timestamp=row.Timestamp;return entity;
    }
    public async Task<T?> GetAsync(string partition,string row,CancellationToken ct)
    {
        try{var response=await _table.GetEntityIfExistsAsync<TableEntity>(partition,row,cancellationToken:ct);return response.HasValue?Read(response.Value):null;}
        catch(RequestFailedException e) when(e.Status==404){return null;}
    }
    public async IAsyncEnumerable<T> ListAsync(string partition,[EnumeratorCancellation]CancellationToken ct)
    {
        if(_createTables)await _table.CreateIfNotExistsAsync(ct);
        await foreach(var row in _table.QueryAsync<TableEntity>(TableClient.CreateQueryFilter($"PartitionKey eq {partition}"),cancellationToken:ct))yield return Read(row);
    }
    public async Task<T> SaveAsync(T entity,bool create,CancellationToken ct)
    {
        if(!create&&(string.IsNullOrEmpty(entity.ETag.ToString())||entity.ETag==ETag.All))throw new InvalidOperationException("A current record version is required.");
        if(create&&_createTables)await _table.CreateIfNotExistsAsync(ct);
        var row=new TableEntity(entity.PartitionKey,entity.RowKey){["Type"]=typeof(T).FullName,["Data"]=JsonSerializer.Serialize(entity,Json)};
        var response=create?await _table.AddEntityAsync(row,ct):await _table.UpdateEntityAsync(row,entity.ETag,TableUpdateMode.Replace,ct);
        entity.ETag=response.Headers.ETag??throw new InvalidOperationException("Storage did not return a record version.");return entity;
    }
}
