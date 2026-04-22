using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class UserContactChangeRequest : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid UserId { get; set; }
        public Guid? TenantId { get; set; }
        public string Channel { get; set; } = string.Empty;
        public string NewContactValue { get; set; } = string.Empty;
        public string NormalizedNewContactValue { get; set; } = string.Empty;
        public string? PreviousContactValue { get; set; }
        public string? ChallengeId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedUtc { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime? VerifiedUtc { get; set; }
        public string? LastError { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
