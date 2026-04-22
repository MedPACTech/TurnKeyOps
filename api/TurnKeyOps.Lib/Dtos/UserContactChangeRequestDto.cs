namespace MedInsights.Lib.Dtos
{
    public sealed class UserContactChangeRequestDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public Guid? TenantId { get; set; }
        public string Channel { get; set; } = string.Empty;
        public string NewContactValueMasked { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime RequestedUtc { get; set; }
        public DateTime? ExpiresUtc { get; set; }
        public DateTime? VerifiedUtc { get; set; }
    }
}
