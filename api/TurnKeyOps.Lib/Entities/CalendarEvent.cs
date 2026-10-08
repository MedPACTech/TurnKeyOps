using Azure.Data.Tables;
using IBeam.Repositories.AzureTables;
using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Enums;

namespace TurnKeyOps.Lib.Entities;

[AzureTableStorageModel(AzureTableStorageModel.Envelope)]
public class CalendarEvent : IEntity, ITableEntity
{
    public List<TurnKeyOps.Lib.Dtos.PortalSlot> CustomerSlots { get; set; } = [];
    public string CustomerResponse { get; set; } = "";
    public Guid? CustomerResponseUserId { get; set; }
    public DateTime? CustomerRespondedAtUtc { get; set; }
    public string CustomerResponseNote { get; set; } = "";
    public string? JobEventType { get; set; }
    public string EventStatus { get; set; } = "scheduled";
    public List<Guid> MembershipIds { get; set; } = [];
    public List<string> ResourceIds { get; set; } = [];
    public bool CustomerVisible { get; set; }
    public Guid Id { get; set; }
    public Guid? LeadId { get; set; }
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public Azure.ETag ETag { get; set; }

    // --- Event info ---
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public CalendarEventType EventType { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool AllDay { get; set; }

    // --- Relationships ---
    public Guid? JobId { get; set; }
    public string? JobName { get; set; }
    public Guid? JobSiteId { get; set; }
    public string? JobSiteName { get; set; }
    public string? LocksmithJobType { get; set; }
    public Guid? AssignedTechnicianMembershipId { get; set; }

    /// <summary>Hex color for calendar display.</summary>
    public string? Color { get; set; }

    // --- Weather snapshot (populated by background refresh) ---
    public string? WeatherSummary { get; set; }
    public int? WeatherTempHigh { get; set; }
    public int? WeatherTempLow { get; set; }
    public int? WeatherPrecipChance { get; set; }
    public string? WeatherIcon { get; set; }

    // --- Audit ---
    public bool IsDeleted { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
    public DateTime DateUpdated { get; set; } = DateTime.UtcNow;
}
