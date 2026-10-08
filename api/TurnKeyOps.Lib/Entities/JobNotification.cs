using Azure;
using Azure.Data.Tables;
namespace TurnKeyOps.Lib.Entities;

// Transport receipt for the existing iBeam communication providers, not a message platform.
public sealed class JobNotification : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public ETag ETag { get; set; }
    public DateTimeOffset? Timestamp { get; set; }
    public Guid JobId { get; set; }
    public Guid ActorId { get; set; }
    public string Channel { get; set; } = "";
    public string Template { get; set; } = "";
    public string Recipient { get; set; } = "";
    public string Body { get; set; } = "";
    public string Status { get; set; } = "prepared";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? AttemptedAtUtc { get; set; }
    public DateTime? ProviderAcceptedAtUtc { get; set; }
}
