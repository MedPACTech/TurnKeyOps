namespace MedInsights.Lib.Dtos
{
    public class TenantSeatEntitlementDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid SubscriptionId { get; set; }
        public int PurchasedSeats { get; set; }
        public int AssignedSeats { get; set; }
        public int ReservedSeats { get; set; }
        public int AvailableSeats { get; set; }
        public int NextRenewalSeatCount { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateUpdated { get; set; }
    }
}
