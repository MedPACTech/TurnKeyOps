using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using MedInsights.AzureServices.Interfaces;
using Microsoft.Extensions.Configuration;
using TurnKeyOps.Lib.Dtos;
namespace TurnKeyOps.Services;
public interface IPortalAccessStore
{
    Task<PortalAccessState> ReadAsync(Guid tenant,CancellationToken ct=default);
    Task SaveAsync(Guid tenant,PortalAccessState state,string expectedVersion,CancellationToken ct=default);
}
// Conditional access metadata; business records are never copied here.
public sealed class PortalAccessStore(IConfiguration configuration,IAzureBlobStorageService blobs):IPortalAccessStore
{
    public const string Container="portal-access";
    private TableClient Table=>new TableServiceClient(configuration["IBeam:Repositories:AzureTables:ConnectionString"]??throw new InvalidOperationException("Portal access storage not configured."))
        .GetTableClient((configuration["IBeam:Repositories:AzureTables:TableNamePrefix"]??"")+"PortalAccessVersions");
    private async Task<TableClient> Ready(CancellationToken ct){var t=Table;if(configuration["IBeam:Repositories:AzureTables:CreateTablesIfNotExists"]!="false")await t.CreateIfNotExistsAsync(ct);return t;}
    public async Task<PortalAccessState> ReadAsync(Guid tenant,CancellationToken ct=default)
    {
        if(tenant==Guid.Empty)throw new UnauthorizedAccessException();
        var table=await Ready(ct);var row=await table.GetEntityIfExistsAsync<TableEntity>(tenant.ToString("N"),"current",cancellationToken:ct);
        if(!row.HasValue)return new();
        var envelope=row.Value ?? throw new InvalidOperationException("Missing portal access pointer.");
        var blob=envelope.GetString("Blob");
        if(blob is null||!blob.StartsWith(tenant.ToString("N")+"/",StringComparison.Ordinal))throw new InvalidOperationException("Invalid portal access storage identity.");
        await using var stream=await blobs.OpenReadAsync(Container,blob,ct);
        var state=await JsonSerializer.DeserializeAsync<PortalAccessState>(stream,JobConfigurationService.Json,ct)??throw new InvalidOperationException("Invalid portal access data.");
        state.Version=envelope.ETag.ToString();return state;
    }
    public async Task SaveAsync(Guid tenant,PortalAccessState state,string expectedVersion,CancellationToken ct=default)
    {
        if(tenant==Guid.Empty||expectedVersion=="*")throw new ArgumentException("A tenant and exact version are required.");
        var table=await Ready(ct);var blob=$"{tenant:N}/{Guid.NewGuid():N}.json";
        await using var stream=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(state,JobConfigurationService.Json));
        await blobs.UploadAsync(Container,blob,stream,"application/json",new Dictionary<string,string>{{"tenantId",tenant.ToString("N")}},ct);
        var row=new TableEntity(tenant.ToString("N"),"current"){{"Blob",blob}};
        try{
            if(string.IsNullOrEmpty(expectedVersion))await table.AddEntityAsync(row,ct);
            else await table.UpdateEntityAsync(row,new ETag(expectedVersion),TableUpdateMode.Replace,ct);
        }catch(RequestFailedException e)when(e.Status is 409 or 412){await blobs.DeleteIfExistsAsync(Container,blob,CancellationToken.None);throw new InvalidOperationException("Portal access changed. Refresh before retrying.");}
        // Do not delete on ambiguous network failure: the pointer might already have committed.
    }
}
