using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Dtos;

namespace MedInsights.Lib.Entities
{
    [AzureTableStorageModel(AzureTableStorageModel.Envelope)]
    public class Patient : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;

        public Guid Id { get; set; }

        [AzureTableProjectedColumn]
        public string FirstName { get; set; } = "";
        [AzureTableProjectedColumn]
        public string LastName { get; set; } = "";
        public DateTime DateOfBirth { get; set; }
        public string Gender { get; set; } = "";

        [AzureTableProjectedColumn]
        public string PatientStatus { get; set; } = "Active";
        public string? PhysicalAddressLine1 { get; set; }
        public string? PhysicalAddressLine2 { get; set; }
        public string? PhysicalCity { get; set; }
        public string? PhysicalState { get; set; }
        public string? PhysicalPostalCode { get; set; }
        public string? PhysicalCountry { get; set; }
        public string? MailingAddressLine1 { get; set; }
        public string? MailingAddressLine2 { get; set; }
        public string? MailingCity { get; set; }
        public string? MailingState { get; set; }
        public string? MailingPostalCode { get; set; }
        public string? MailingCountry { get; set; }
        public string? BillingAddressLine1 { get; set; }
        public string? BillingAddressLine2 { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingState { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
        public string? PreFacilityPhysicalAddressLine1 { get; set; }
        public string? PreFacilityPhysicalAddressLine2 { get; set; }
        public string? PreFacilityPhysicalCity { get; set; }
        public string? PreFacilityPhysicalState { get; set; }
        public string? PreFacilityPhysicalPostalCode { get; set; }
        public string? PreFacilityPhysicalCountry { get; set; }
        [AzureTableProjectedColumn]
        public Guid? CurrentFacilityId { get; set; }
        public string? CurrentFacilityName { get; set; }
        public DateTime? CurrentFacilityAdmitDate { get; set; }
        public string? CurrentFacilityStatus { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }
        public bool IsDeleted { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
