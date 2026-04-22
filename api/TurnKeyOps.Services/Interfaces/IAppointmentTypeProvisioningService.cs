namespace MedInsights.Services.Interfaces
{
    public interface IAppointmentTypeProvisioningService
    {
        Task EnsureTenantHasActiveAppointmentTypesAsync(Guid tenantId, CancellationToken ct = default);
    }
}
