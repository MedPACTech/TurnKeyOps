using Azure.Data.Tables;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;

namespace TurnKeyOps.Lib.Entities;

[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public class EstimateLineItem : IEntity, ITableEntity
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    public Guid EstimateId { get; set; }
    public int SortOrder { get; set; }

    // --- Line item ---
    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public double Quantity { get; set; }
    public string Unit { get; set; } = "ea";
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    /// <summary>
    /// True if this line was auto-calculated (e.g. concrete CY formula).
    /// </summary>
    public bool IsCalculated { get; set; }

    public string? Notes { get; set; }

    // --- Audit ---
    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
