using Azure.Data.Tables;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Lib.Entities;

[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public class Job : IEntity, ITableEntity
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    // --- Core ---
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TradeType TradeType { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Lead;

    // --- Relationships ---
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? JobSiteId { get; set; }
    public string? JobSiteName { get; set; }
    public Guid? EstimateId { get; set; }
    public string? EstimateNumber { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? QuoteRequestId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? ProjectAddress { get; set; }
    public string? ProjectName { get; set; }
    public string? EstimateSnapshotBlobName { get; set; }
    public string? EstimateSnapshotJson { get; set; }
    public string? WorkflowPayloadBlobName { get; set; }

    // --- Scheduling ---
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public DateTime? ActualStart { get; set; }
    public DateTime? ActualEnd { get; set; }
    public string? Crew { get; set; }

    // --- Financials ---
    public decimal EstimatedTotal { get; set; }
    public decimal InvoicedTotal { get; set; }
    public decimal PaidTotal { get; set; }
    public decimal RequiredDepositPercent { get; set; }

    public string? Notes { get; set; }

    // --- Audit ---
    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
