using Azure.Data.Tables;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Lib.Entities;

[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public class Estimate : IEntity, ITableEntity
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    // --- Core ---
    public string EstimateNumber { get; set; } = string.Empty;
    public EstimateStatus Status { get; set; } = EstimateStatus.Draft;
    public TradeType TradeType { get; set; }

    // --- Relationships ---
    public Guid? AppointmentId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerCompany { get; set; }
    public Guid? JobId { get; set; }
    public string? JobName { get; set; }
    public Guid? JobSiteId { get; set; }
    public Guid? ConvertedJobId { get; set; }
    public string? ProjectAddress { get; set; }
    public string? EstimatorName { get; set; }
    public string? ProjectName { get; set; }

    // --- Financials ---
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }

    // --- Concrete-specific summary ---
    public double? TotalSqft { get; set; }
    public double? DepthInches { get; set; }
    public double? CubicYards { get; set; }
    public int? NumberOfPours { get; set; }

    // --- Framing-specific summary ---
    public double? WallLinearFeet { get; set; }
    public int? StudCount { get; set; }

    // --- Dates ---
    public DateTime? SentDate { get; set; }
    public DateTime? SubmittedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? AcceptedDate { get; set; }

    // --- Signature ---
    public string? SignatureDataUrl { get; set; }
    public string? SignedByName { get; set; }
    public DateTime? SignedDate { get; set; }

    public string? Notes { get; set; }
    public string? StructuredInputBlobName { get; set; }
    public string? CalculationSnapshotBlobName { get; set; }
    public string? BobTranscriptBlobName { get; set; }
    public string? StructuredInputJson { get; set; }
    public string? CalculationSnapshotJson { get; set; }
    public DateTime? RevisedDate { get; set; }
    public DateTime? AwardedDate { get; set; }
    public DateTime? RejectedDate { get; set; }
    public DateTime? ConvertedToJobDate { get; set; }

    // --- Audit ---
    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
