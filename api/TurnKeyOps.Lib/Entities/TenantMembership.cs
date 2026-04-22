using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class TenantMembership : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }

        [AzureTableProjectedColumn]
        public string Role { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public string MembershipStatus { get; set; } = string.Empty;

        [AzureTableProjectedColumn]
        public string SeatStatus { get; set; } = string.Empty;

        public string? InvitedEmail { get; set; }
        public string? InvitedPhone { get; set; }
        public string? VerifiedJoinChannel { get; set; }
        public bool IsOwner { get; set; }
        public bool IsBillingAdmin { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateInvited { get; set; }
        public DateTime? DateJoined { get; set; }
        public DateTime? DateRemoved { get; set; }
        public DateTime? DateUpdated { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
