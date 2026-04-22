using Azure.Data.Tables;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Lib.Entities;

/// <summary>
/// Reusable estimate templates with default line items per trade.
/// Seeded at startup for concrete + framing.
/// </summary>
[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public class EstimateTemplate : IEntity, ITableEntity
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    public string Name { get; set; } = string.Empty;
    public TradeType TradeType { get; set; }
    public string? Description { get; set; }

    /// <summary>JSON-serialized default line items.</summary>
    public string? DefaultLineItemsJson { get; set; }

    public bool IsSystemTemplate { get; set; }

    // --- Audit ---
    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
