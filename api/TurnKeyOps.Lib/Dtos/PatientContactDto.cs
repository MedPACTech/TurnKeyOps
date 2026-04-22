using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Dtos
{
    public class PatientContactDto
    {
        public Guid Id { get; set; } = default!;
        [Required] public Guid PatientId { get; set; } = default!;
        [Required] public ContactType ContactType { get; set; } = default!;
        [Required] public PatientRelationship Relationship { get; set; } = default!;
        public string? OtherRelationship { get; set; }
        public bool IsPrimary { get; set; }
        public bool IsSecondary { get; set; }

        // Identity
        [Required] public string FirstName { get; set; } = default!;
        [Required] public string LastName { get; set; } = default!;
        public string? MiddleName { get; set; }
        public string? OrganizationName { get; set; }

        // Communication
        public string? PrimaryPhone { get; set; }
        public string? SecondaryPhone { get; set; }
        [EmailAddress] public string? Email { get; set; }

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
