namespace MedInsights.Lib.Dtos
{
    public class PlatformUserDto
    {
        public Guid Id { get; set; }
        public string? PrimaryEmail { get; set; }
        public string? PrimaryPhone { get; set; }
        public bool EmailVerified { get; set; }
        public bool PhoneVerified { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
