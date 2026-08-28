using MedInsights.Lib.Configurations;

namespace MedInsights.Services.Interfaces;

public interface ITenantCommunicationProfileResolver
{
    TenantCommunicationProfile Resolve(Guid tenantId);
}
