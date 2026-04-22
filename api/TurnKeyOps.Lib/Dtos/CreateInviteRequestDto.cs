namespace MedInsights.Lib.Dtos
{
    public sealed class CreateInviteRequestDto
    {
        public string? InvitedEmail { get; set; }
        public string? InvitedPhone { get; set; }
        public string Role { get; set; } = string.Empty;
        public DateTime? ExpiresAtUtc { get; set; }
    }
}
