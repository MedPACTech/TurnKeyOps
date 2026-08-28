using MedInsights.Lib.Configurations;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.Services;

public sealed class TenantCommunicationProfileResolver : ITenantCommunicationProfileResolver
{
    private readonly IReadOnlyDictionary<Guid, TenantCommunicationProfile> _profiles;

    public TenantCommunicationProfileResolver(IOptions<ProductionCommunicationOptions> options)
    {
        _profiles = options.Value.Tenants.Values
            .Where(profile => profile.TenantId != Guid.Empty)
            .ToDictionary(profile => profile.TenantId);
    }

    public TenantCommunicationProfile Resolve(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (_profiles.TryGetValue(tenantId, out var profile))
            return profile;

        throw new InvalidOperationException(
            $"No production communication profile is configured for tenant '{tenantId:D}'.");
    }
}
