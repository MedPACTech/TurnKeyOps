namespace MedInsights.Lib.Dtos
{
    public sealed class TenantOnboardingPolicyDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public bool ReserveSeatAtInviteTime { get; set; } = true;
        public bool AutoAssignSeatOnActivation { get; set; } = true;
        public int DefaultInviteExpiryDays { get; set; } = 7;
        public string ExpiredInviteHandling { get; set; } = "cancel_and_release_seat";
        public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
