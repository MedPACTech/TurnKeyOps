using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace TurnKeyOps.Lib.Entities;

[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public sealed class QuoteEstimate : IEntity, ITableEntity
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }
    public Guid QuoteRequestId { get; set; }
    public int RevisionNumber { get; set; } = 1;
    public string Status { get; set; } = "draft";
    public string? DeliveryStatus { get; set; }
    public string PayloadBlobName { get; set; } = string.Empty;
    public string? CustomerAccessTokenHash { get; set; }
    public DateTime? AccessTokenExpiresAtUtc { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
