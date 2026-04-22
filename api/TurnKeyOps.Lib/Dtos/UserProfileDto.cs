
namespace MedInsights.Lib.Dtos
{
    public class UserProfileDto
    {
        public Guid Id { get; set; } = default!;
        public Guid? TenantId { get; set; } = default!;
        public string FirstName { get; set; } = default!;
        public string LastName { get; set; } = default!;
        public string? PrimaryPhone { get; set; } = default!;
        public string? PrimaryEmail { get; set; } = default!;
        public string? SecondaryPhone { get; set; } = default!;
        public string? SecondaryEmail { get; set; } = default!;
        public string? AddressLine1 { get; set; } = default!;
        public string? AddressLine2 { get; set; } = default!;
        public string? City { get; set; } = default!;
        public string? State { get; set; } = default!;
        public string? PostalCode { get; set; } = default!;
        public string? Title { get; set; } = default!;
        public string? Suffix { get; set; } = default!;
        public bool IsActive { get; set; } = default!;
    }
}
