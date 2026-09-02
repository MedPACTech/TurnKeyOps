using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace TurnKeyOps.Lib.Entities;

[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public sealed class TenantSettingsDocument : IEntity, ITableEntity
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    public Guid TenantId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public int SchemaVersion { get; set; } = 1;
    public bool IsPublic { get; set; }
    public string ValuesJson { get; set; } = "{}";
    public string SecretReferencesJson { get; set; } = "{}";
    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
