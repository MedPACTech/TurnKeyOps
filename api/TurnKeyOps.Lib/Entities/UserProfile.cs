using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class UserProfile : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = string.Empty;
        public string RowKey { get; set; } = string.Empty;

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public Guid ApplicationUserId { get; set; }


        [AzureTableProjectedColumn]
        public string FirstName { get; set; } = string.Empty;
        [AzureTableProjectedColumn]
        public string LastName { get; set; } = string.Empty;
        public string? PrimaryPhone { get; set; }
        public string? PrimaryEmail { get; set; }
        public string? SecondaryPhone { get; set; }
        public string? SecondaryEmail { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Title { get; set; }
        public string? Suffix { get; set; }

        [AzureTableProjectedColumn]
        public bool IsActive { get; set; }

        [AzureTableProjectedColumn]
        public string Role { get; set; }

        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
    }
}
