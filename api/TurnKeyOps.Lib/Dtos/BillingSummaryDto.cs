namespace MedInsights.Lib.Dtos
{
    public sealed class BillingSummaryDto
    {
        public TenantBillingAccountDto? BillingAccount { get; set; }
        public TenantSubscriptionDto? Subscription { get; set; }
        public TenantSeatEntitlementDto? SeatEntitlement { get; set; }
        public TenantCreditBalanceDto? CreditBalance { get; set; }
        public int ActiveAssignedUsers { get; set; }
        public int PendingInvites { get; set; }
    }
}
