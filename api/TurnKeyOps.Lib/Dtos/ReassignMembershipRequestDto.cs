namespace MedInsights.Lib.Dtos
{
    public sealed class ReassignMembershipRequestDto
    {
        public string? InvitedEmail { get; set; }
        public string? InvitedPhone { get; set; }
        public string? Role { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }
}
