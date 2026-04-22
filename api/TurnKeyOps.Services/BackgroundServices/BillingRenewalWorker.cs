using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedInsights.Services.BackgroundServices
{
    public sealed class BillingRenewalWorker : BackgroundService
    {
        private static readonly TimeSpan PollInterval = TimeSpan.FromHours(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<BillingRenewalWorker> _logger;

        public BillingRenewalWorker(IServiceScopeFactory scopeFactory, ILogger<BillingRenewalWorker> logger)
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
                var seatEntitlementService = scope.ServiceProvider.GetRequiredService<ITenantSeatEntitlementService>();
                var creditAccountingService = scope.ServiceProvider.GetRequiredService<ICreditAccountingService>();
                var alertService = scope.ServiceProvider.GetRequiredService<IOperationalAlertService>();
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

                var dueSubscriptions = await subscriptionRepository.GetRenewalDueAsync(DateTime.UtcNow, ct);
                foreach (var subscription in dueSubscriptions)
                {
                    try
                    {
                        await creditAccountingService.ExpirePurchasedCreditsAsync(
                            subscription.TenantId,
                            DateTime.UtcNow,
                            $"renewal:{subscription.Id:D}",
                            "Expired purchased credits at contract renewal.",
                            ct);

                        await seatEntitlementService.ApplyRenewalAsync(subscription.TenantId, ct);

                        var nextTermStartUtc = subscription.TermEndUtc;
                        var nextTermEndUtc = AdvanceTermEnd(subscription.TermEndUtc, subscription.BillingCadence);

                        subscription.TermStartUtc = nextTermStartUtc;
                        subscription.TermEndUtc = nextTermEndUtc;
                        subscription.DateUpdated = DateTime.UtcNow;
                        await subscriptionRepository.SaveAsync(subscription, ct);

                        await creditAccountingService.EnsureTenantBalanceAsync(
                            subscription.TenantId,
                            nextTermStartUtc,
                            nextTermEndUtc,
                            null,
                            ct);
                        await auditService.RecordAsync(new MedInsights.Lib.Dtos.RecordAuditEventRequestDto
                        {
                            TenantId = subscription.TenantId,
                            Category = "billing",
                            Action = "contract_renewed",
                            Severity = "info",
                            TargetType = "subscription",
                            TargetId = subscription.Id.ToString("D"),
                            Source = nameof(BillingRenewalWorker),
                            Description = "Processed contract renewal and applied renewal state."
                        }, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed renewal processing for tenant {TenantId} subscription {SubscriptionId}.", subscription.TenantId, subscription.Id);
                        await alertService.RaiseAsync(new MedInsights.Lib.Dtos.RaiseOperationalAlertRequestDto
                        {
                            TenantId = subscription.TenantId,
                            AlertType = "renewal_issue",
                            Severity = "error",
                            DedupeKey = $"renewal-issue:{subscription.Id:D}",
                            Source = nameof(BillingRenewalWorker),
                            Message = $"Renewal processing failed for subscription {subscription.Id:D}.",
                            ContextJson = ex.ToString()
                        }, ct);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Billing renewal worker cycle failed.");
            }
        }

        private static DateTime AdvanceTermEnd(DateTime currentEndUtc, string? billingCadence)
            => billingCadence?.Trim().ToLowerInvariant() switch
            {
                "annual" => currentEndUtc.AddYears(1),
                "quarterly" => currentEndUtc.AddMonths(3),
                _ => currentEndUtc.AddMonths(1)
            };
    }
}
