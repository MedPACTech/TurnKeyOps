namespace MedInsights.Lib.Dtos
{
    public class InviteDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ReservedSeatMembershipId { get; set; }
        public Guid SentByMembershipId { get; set; }
        public Guid? RedeemedByUserId { get; set; }
        public string? InvitedEmail { get; set; }
        public string? InvitedPhone { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? InviteToken { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateRedeemed { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
