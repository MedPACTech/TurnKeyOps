using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Converters;
using MedInsights.Lib.Enums;
using System.Text.Json.Serialization;

namespace MedInsights.Lib.Entities
{
    /// <summary>
    /// PatientContact entity stored in Azure Table Storage.
    /// PartitionKey = TenantId|PatientId
    /// RowKey       = ContactId (GUID as string)
    /// </summary>
    public class PatientContact : IEntity, ITableEntity
    {
        // Required Azure Table fields
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        // IEntity fields
        public Guid Id { get; set; }
        public bool IsDeleted { get; set; } = false;

        // Linkage
        public Guid PatientId { get; set; } = default!;
        [JsonConverter(typeof(ContactTypeJsonConverter))]
        public ContactType ContactType { get; set; } = ContactType.Other;
        public PatientRelationship Relationship { get; set; } = PatientRelationship.Other;
        public string? OtherRelationship { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsSecondary { get; set; }

        // Identity
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string? MiddleName { get; set; }
        public string? OrganizationName { get; set; }

        // Communication
        public string? PrimaryPhone { get; set; }
        public string? SecondaryPhone { get; set; }
        public string? Email { get; set; }

        // Address
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }

        // Preferences & notes
        public string? PreferredContactMethod { get; set; }
        public string? Notes { get; set; }

        // Disclosure permissions
        public bool HasHIPAAPermission { get; set; }
        public bool HasBillingPermission { get; set; }

        // Legal & PoA
        public bool HasDurablePowerOfAttorney { get; set; }
        public bool HasMedicalPowerOfAttorney { get; set; }
        public bool HasFinancialPowerOfAttorney { get; set; }
    }
}
