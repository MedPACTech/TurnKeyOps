namespace MedInsights.Lib.Dtos
{
    public sealed class InviteRepairReportDto
    {
        public bool Applied { get; set; }
        public Guid TenantId { get; set; }
        public int PendingInviteCount { get; set; }
        public int ReservedMembershipCount { get; set; }
        public int OrphanedInviteCount { get; set; }
        public int StrandedReservedMembershipCount { get; set; }
        public int CancelledInviteCount { get; set; }
        public int ReleasedMembershipCount { get; set; }
        public bool SeatEntitlementAdjusted { get; set; }
        public TenantSeatEntitlementDto? SeatEntitlementBefore { get; set; }
        public TenantSeatEntitlementDto? SeatEntitlementAfter { get; set; }
        public IReadOnlyList<string> Findings { get; set; } = [];
    }
}
