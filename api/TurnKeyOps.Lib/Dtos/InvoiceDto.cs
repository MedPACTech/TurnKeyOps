using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Lib.Dtos;

public class InvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public Guid? JobId { get; set; }
    public string? JobName { get; set; }
    public Guid? EstimateId { get; set; }
    public Guid? QuoteRequestId { get; set; }
    public int EstimateRevisionNumber { get; set; }

    public decimal Subtotal { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal BalanceDue { get; set; }

    public DateTime IssueDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? PaidDate { get; set; }
    public DateTime? SentAtUtc { get; set; }

    public string? StripePaymentUrl { get; set; }
    public string? Notes { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? SiteName { get; set; }
    public string? ServiceSummary { get; set; }
    public string? ReviewUrl { get; set; }
    public decimal RequiredDepositPercent { get; set; } = 50m;
    public List<InvoiceLineItemDto> LineItems { get; set; } = [];
    public List<string> ScopeLineItems { get; set; } = [];
    public List<InvoicePaymentDto> Payments { get; set; } = [];
    public List<InvoiceReminderDto> Reminders { get; set; } = [];
    public List<InvoiceAuditEventDto> AuditEvents { get; set; } = [];
    public InvoiceJobReleaseDto JobRelease { get; set; } = new();
    public string Version { get; set; } = string.Empty;
    public DateTime? DateCreated { get; set; }
    public DateTime? DateUpdated { get; set; }
}

public sealed class InvoicePaymentDto
{
    public Guid Id { get; set; }
    public string Kind { get; set; } = "payment";
    public string Status { get; set; } = "succeeded";
    public decimal Amount { get; set; }
    public string Method { get; set; } = "ACH";
    public string? Note { get; set; }
    public string? Provider { get; set; }
    public string? ExternalReference { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
}

public sealed class InvoiceReminderDto
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = "email";
    public string Recipient { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime SentAtUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
}

public sealed class InvoiceAuditEventDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string Actor { get; set; } = string.Empty;
}

public sealed class InvoiceJobReleaseDto
{
    public bool IsEligible { get; set; }
    public decimal RequiredDepositAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal RemainingDepositAmount { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed class InvoicePaymentInputDto
{
    public string? Kind { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "ACH";
    public string? Note { get; set; }
    public string? Provider { get; set; }
    public string? ExternalReference { get; set; }
    public string? IdempotencyKey { get; set; }
    public string Status { get; set; } = "succeeded";
    public string? ExpectedVersion { get; set; }
}

public sealed class InvoiceLineItemDto
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public int SortOrder { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1m;
    public string Unit { get; set; } = "ea";
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class InvoiceReminderInputDto
{
    public string Channel { get; set; } = "email";
    public string? Recipient { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ExpectedVersion { get; set; }
}

public sealed class InvoiceMutationInputDto
{
    public string? ExpectedVersion { get; set; }
}

public sealed class InvoiceWorkflowPayloadDto
{
    public DateTime? SentAtUtc { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? SiteName { get; set; }
    public string? ServiceSummary { get; set; }
    public string? ReviewUrl { get; set; }
    public decimal RequiredDepositPercent { get; set; } = 50m;
    public List<string> ScopeLineItems { get; set; } = [];
    public List<InvoicePaymentDto> Payments { get; set; } = [];
    public List<InvoiceReminderDto> Reminders { get; set; } = [];
    public List<InvoiceAuditEventDto> AuditEvents { get; set; } = [];
}

public sealed class InvoiceProviderWebhookResultDto
{
    public string Provider { get; set; } = string.Empty;
    public string EventId { get; set; } = string.Empty;
    public InvoiceDto Invoice { get; set; } = new();
}
