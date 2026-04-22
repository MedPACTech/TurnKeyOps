namespace MedInsights.Lib.Dtos
{
    public class TenantProfileDto
    {
        public Guid Id { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string? LogoUrl { get; set; }
        public string? Website { get; set; }
        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public string? PointOfContactName { get; set; }
        public string? PointOfContactEmail { get; set; }
        public string? PointOfContactPhone { get; set; }
        public string? BusinessLegalName { get; set; }
        public string? BillingContactName { get; set; }
        public string? BillingContactEmail { get; set; }
        public string? BillingContactPhone { get; set; }
        public string? BillingEmail { get; set; }
        public string? BillingAddressLine1 { get; set; }
        public string? BillingAddressLine2 { get; set; }
        public string? BillingCity { get; set; }
        public string? BillingState { get; set; }
        public string? BillingPostalCode { get; set; }
        public string? BillingCountry { get; set; }
        public string? TaxRegistrationNumber { get; set; }
        public string? TaxRegion { get; set; }
        public bool IsTaxExempt { get; set; }
        public string? EnterpriseAccountNumber { get; set; }
        public string? EnterpriseCustomerCode { get; set; }
        public string? PurchaseOrderNumber { get; set; }
        public Guid? DefaultNoteTypeId { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
