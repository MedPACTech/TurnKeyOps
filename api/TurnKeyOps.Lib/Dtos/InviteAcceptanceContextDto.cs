namespace MedInsights.Lib.Dtos
{
    public sealed class InviteAcceptanceContextDto
    {
        public Guid InviteId { get; set; }
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime ExpiresAtUtc { get; set; }
        public string? InvitedEmailMasked { get; set; }
        public string? InvitedPhoneMasked { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool RequiresAuthentication { get; set; }
        public bool CanRedeem { get; set; }
        public string NextStep { get; set; } = string.Empty;
        public bool AuthenticatedUserMatchesInvite { get; set; }
        public string? MatchedVerifiedContactChannel { get; set; }
        public bool AuthenticatedUserAlreadyMember { get; set; }
        public string? AuthenticatedUserMembershipStatus { get; set; }
    }
}
