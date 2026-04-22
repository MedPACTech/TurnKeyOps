namespace MedInsights.Services.Interfaces
{
    public interface ITenantMembershipAuthorizationService
    {
        Task RequireBillingAccessAsync(CancellationToken ct = default);
        Task RequireMembershipManagementAccessAsync(CancellationToken ct = default);
    }
}
