using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class Invite : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid TenantId { get; set; }
        public Guid ReservedSeatMembershipId { get; set; }
        public Guid SentByMembershipId { get; set; }
        public Guid? RedeemedByUserId { get; set; }

        [AzureTableProjectedColumn]
        public string? InvitedEmail { get; set; }

        [AzureTableProjectedColumn]
        public string? InvitedPhone { get; set; }

        public string InviteTokenHash { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateRedeemed { get; set; }
        public DateTime? DateUpdated { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
