using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Dtos
{
    public class PatientDto
    {
        public Guid Id { get; set; } = default!;        // patientId_ticks_recordId
        
        // Patient-specific fields
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public Guid PatientId { get; set; }
        
        [JsonConverter(typeof(DateOnlyConverter))]
        public DateOnly DateOfBirth { get; set; }
        public string Gender { get; set; } = "";
        public string PatientStatus { get; set; } = "Active";
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool HasHIPAAPermission { get; set; }
        public bool HasBillingPermission { get; set; }
        public string? PrimaryFirstName { get; set; }
        public string? PrimaryLastName { get; set; }
        public PatientRelationship? Relationship { get; set; }
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
        public Guid? CurrentFacilityId { get; set; }
        public string? CurrentFacilityName { get; set; }
        public DateTime? CurrentFacilityAdmitDate { get; set; }
        public string? CurrentFacilityStatus { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }

    }
}
