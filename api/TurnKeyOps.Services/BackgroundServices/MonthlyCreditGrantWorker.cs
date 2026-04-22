using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedInsights.Services.BackgroundServices
{
    public sealed class MonthlyCreditGrantWorker : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromHours(6);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MonthlyCreditGrantWorker> _logger;

        public MonthlyCreditGrantWorker(IServiceScopeFactory scopeFactory, ILogger<MonthlyCreditGrantWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(PollInterval);

            while (!stoppingToken.IsCancellationRequested)
            {
                await RunCycleAsync(stoppingToken);

                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
        }

        private async Task RunCycleAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var subscriptionRepository = scope.ServiceProvider.GetRequiredService<ITenantSubscriptionRepository>();
                var membershipRepository = scope.ServiceProvider.GetRequiredService<ITenantMembershipRepository>();
                var pricingRuleSnapshotRepository = scope.ServiceProvider.GetRequiredService<IPricingRuleSnapshotRepository>();
                var userCreditPeriodRepository = scope.ServiceProvider.GetRequiredService<IUserCreditPeriodRepository>();
                var creditAccountingService = scope.ServiceProvider.GetRequiredService<ICreditAccountingService>();

                var subscriptions = await subscriptionRepository.GetAllActiveAsync(ct);
                var monthStartUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var monthEndUtc = monthStartUtc.AddMonths(1);
                var usagePeriodKey = monthStartUtc.ToString("yyyy-MM");

                foreach (var subscription in subscriptions)
                {
                    try
                    {
                        if (subscription.TermEndUtc <= DateTime.UtcNow || subscription.TermStartUtc > DateTime.UtcNow)
                            continue;

                        var includedCreditsPerSeat = await ResolveIncludedCreditsPerSeatAsync(subscription, pricingRuleSnapshotRepository, ct);
                        if (includedCreditsPerSeat <= 0)
                            continue;

                        var memberships = await membershipRepository.GetActiveAssignedByTenantAsync(subscription.TenantId, ct);
                        foreach (var membership in memberships)
                        {
                            var existing = await userCreditPeriodRepository.GetAsync(
                                $"TENANT={subscription.TenantId:N}|PERIOD={usagePeriodKey}",
                                $"USER={membership.UserId:N}",
                                ct);

                            if (existing is not null && existing.IncludedCreditsGranted >= includedCreditsPerSeat)
                                continue;

                            var delta = includedCreditsPerSeat - (existing?.IncludedCreditsGranted ?? 0);
                            if (delta <= 0)
                                continue;

                            await creditAccountingService.GrantIncludedCreditsAsync(
                                subscription.TenantId,
                                membership.UserId,
                                usagePeriodKey,
                                delta,
                                null,
                                $"monthly-grant:{subscription.Id:D}:{usagePeriodKey}",
                                "Granted monthly included credits.",
                                DateTime.UtcNow,
                                ct);
                        }

                        await creditAccountingService.EnsureTenantBalanceAsync(
                            subscription.TenantId,
                            subscription.TermStartUtc,
                            subscription.TermEndUtc,
                            null,
                            ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed monthly credit grants for tenant {TenantId} subscription {SubscriptionId}.", subscription.TenantId, subscription.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Monthly credit grant worker cycle failed.");
            }
        }

        private static async Task<int> ResolveIncludedCreditsPerSeatAsync(
            TenantSubscription subscription,
            IPricingRuleSnapshotRepository pricingRuleSnapshotRepository,
            CancellationToken ct)
        {
            if (!subscription.PricingRuleSnapshotId.HasValue || subscription.PricingRuleSnapshotId.Value == Guid.Empty)
                return 0;

            var snapshot = await pricingRuleSnapshotRepository.GetAsync(
                EntityKeyPolicy.TenantPartition(subscription.TenantId),
                EntityKeyPolicy.Row(subscription.PricingRuleSnapshotId.Value),
                ct);

            return snapshot?.IncludedCreditsPerSeatPerMonth ?? 0;
        }
    }
}
