using Azure.Data.Tables;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Lib.Entities;

[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public class Invoice : IEntity, ITableEntity
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    // --- Core ---
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;

    // --- Relationships ---
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? JobId { get; set; }
    public string? JobName { get; set; }
    public Guid? EstimateId { get; set; }

    // --- Financials ---
    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }

    // --- Dates ---
    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }

    // --- Payment ---
    public string? StripePaymentIntentId { get; set; }
    public string? StripePaymentUrl { get; set; }

    public string? Notes { get; set; }

    // --- Audit ---
    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
