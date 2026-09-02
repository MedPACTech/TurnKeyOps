using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace TurnKeyOps.Lib.Entities;

[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public sealed class QuoteRequest : IEntity, ITableEntity
{
    public Guid Id { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    public Guid TenantId { get; set; }
    public DateTime SubmittedAtUtc { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string ServiceAddress { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public string RequestedTimeline { get; set; } = string.Empty;
    public string Priority { get; set; } = "standard";
    public string Need { get; set; } = string.Empty;
    public string Source { get; set; } = "public-site";
    public string Status { get; set; } = "new";
    public string AssignedTo { get; set; } = string.Empty;
    public string NextAction { get; set; } = string.Empty;
    public string IntakeSummary { get; set; } = string.Empty;
    public string AttachmentsJson { get; set; } = "[]";
    public string QualificationJson { get; set; } = "{}";
    public string SubmittedPayloadJson { get; set; } = "{}";
    public string TimelineJson { get; set; } = "[]";
    public string? SiteVisitScheduleJson { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateUpdated { get; set; }
}
