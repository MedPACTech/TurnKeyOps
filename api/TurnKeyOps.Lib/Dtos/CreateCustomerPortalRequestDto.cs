namespace MedInsights.Lib.Dtos
{
    public sealed class CreateCustomerPortalRequestDto
    {
        public string? Provider { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public Guid? TenantId { get; set; }
    }
}
