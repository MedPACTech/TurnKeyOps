using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class AppointmentTypeStartupSeedContributor : IStartupSeedContributor
    {
        private readonly ITenantProfileRepository _tenantProfileRepository;
        private readonly IUserProfileRepository _userProfileRepository;
        private readonly ITenantMembershipRepository _tenantMembershipRepository;
        private readonly ITenantSubscriptionRepository _tenantSubscriptionRepository;
        private readonly IAppointmentTypeProvisioningService _appointmentTypeProvisioningService;

        public AppointmentTypeStartupSeedContributor(
            ITenantProfileRepository tenantProfileRepository,
            IUserProfileRepository userProfileRepository,
            ITenantMembershipRepository tenantMembershipRepository,
            ITenantSubscriptionRepository tenantSubscriptionRepository,
            IAppointmentTypeProvisioningService appointmentTypeProvisioningService)
        {
            _tenantProfileRepository = tenantProfileRepository;
            _userProfileRepository = userProfileRepository;
            _tenantMembershipRepository = tenantMembershipRepository;
            _tenantSubscriptionRepository = tenantSubscriptionRepository;
            _appointmentTypeProvisioningService = appointmentTypeProvisioningService;
        }

        public async Task SeedAsync(CancellationToken ct = default)
        {
            var tenantIds = new HashSet<Guid>();

            foreach (var tenantId in await _tenantProfileRepository.GetTenantIdsAsync(ct))
                tenantIds.Add(tenantId);
            foreach (var tenantId in await _userProfileRepository.GetTenantIdsAsync(ct))
                tenantIds.Add(tenantId);
            foreach (var tenantId in await _tenantMembershipRepository.GetTenantIdsAsync(ct))
                tenantIds.Add(tenantId);
            foreach (var subscription in await _tenantSubscriptionRepository.GetAllActiveAsync(ct))
            {
                if (subscription.TenantId != Guid.Empty)
                    tenantIds.Add(subscription.TenantId);
            }

            foreach (var tenantId in tenantIds)
            {
                await _appointmentTypeProvisioningService.EnsureTenantHasActiveAppointmentTypesAsync(tenantId, ct);
            }
        }
    }
}
