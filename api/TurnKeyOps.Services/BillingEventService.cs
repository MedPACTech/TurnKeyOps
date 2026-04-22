using System.Text.Json;
using MedInsights.Lib;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class BillingEventService : IBillingEventService
    {
        private readonly ITenantBillingAccountRepository _billingAccountRepository;
        private readonly ITenantSubscriptionRepository _subscriptionRepository;
        private readonly ITenantCreditBalanceRepository _creditBalanceRepository;
        private readonly ITenantSeatEntitlementService _seatEntitlementService;
        private readonly IBillingLedgerRepository _billingLedgerRepository;
        private readonly ICreditAccountingService _creditAccountingService;
        private readonly IPaymentProviderResolver _paymentProviderResolver;
        private readonly IOperationalAlertService _alertService;
        private readonly IAuditService _auditService;

        public BillingEventService(
            ITenantBillingAccountRepository billingAccountRepository,
            ITenantSubscriptionRepository subscriptionRepository,
            ITenantCreditBalanceRepository creditBalanceRepository,
            ITenantSeatEntitlementService seatEntitlementService,
            IBillingLedgerRepository billingLedgerRepository,
            ICreditAccountingService creditAccountingService,
            IPaymentProviderResolver paymentProviderResolver,
            IOperationalAlertService alertService,
            IAuditService auditService)
        {
            _billingAccountRepository = billingAccountRepository;
            _subscriptionRepository = subscriptionRepository;
            _creditBalanceRepository = creditBalanceRepository;
            _seatEntitlementService = seatEntitlementService;
            _billingLedgerRepository = billingLedgerRepository;
            _creditAccountingService = creditAccountingService;
            _paymentProviderResolver = paymentProviderResolver;
            _alertService = alertService;
            _auditService = auditService;
        }

        public async Task HandleWebhookAsync(PaymentWebhookEventDto dto, CancellationToken ct = default)
        {
            switch (dto.EventType)
            {
                case "checkout.session.completed":
                case "CHECKOUT.ORDER.APPROVED":
                case "PAYMENT.CAPTURE.COMPLETED":
                    await HandleCheckoutCompletedAsync(dto, ct);
                    break;
                case "customer.subscription.created":
                case "customer.subscription.updated":
                case "customer.subscription.deleted":
                case "BILLING.SUBSCRIPTION.ACTIVATED":
                case "BILLING.SUBSCRIPTION.UPDATED":
                case "BILLING.SUBSCRIPTION.CANCELLED":
                case "BILLING.SUBSCRIPTION.SUSPENDED":
                    await HandleSubscriptionChangedAsync(dto, ct);
                    break;
                case "invoice.paid":
                case "invoice.payment_failed":
                case "PAYMENT.SALE.COMPLETED":
                case "PAYMENT.SALE.DENIED":
                    await HandleInvoiceStatusAsync(dto, ct);
                    break;
            }
        }

        private async Task HandleCheckoutCompletedAsync(PaymentWebhookEventDto dto, CancellationToken ct)
        {
            var tenantId = await ResolveTenantIdAsync(dto, ct);
            if (!tenantId.HasValue || string.IsNullOrWhiteSpace(dto.CustomerId))
                return;

            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId.Value);
            var billingAccount = await _billingAccountRepository.GetAsync(partitionKey, "BILLING", ct);
            if (billingAccount is null)
            {
                billingAccount = new TenantBillingAccount
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId.Value,
                    PartitionKey = partitionKey,
                    RowKey = "BILLING",
                    Provider = dto.Provider,
                    BillingStatus = MapBillingStatus(dto.EventType, dto.Status),
                    ProviderCustomerId = dto.CustomerId,
                    DateCreated = DateTime.UtcNow,
                    DateUpdated = DateTime.UtcNow,
                    IsDeleted = false
                };
            }
            else
            {
                billingAccount.Provider = dto.Provider;
                billingAccount.ProviderCustomerId = dto.CustomerId;
                billingAccount.BillingStatus = MapBillingStatus(dto.EventType, dto.Status);
                billingAccount.DateUpdated = DateTime.UtcNow;
            }

            await _billingAccountRepository.SaveAsync(billingAccount, ct);

            if (!string.IsNullOrWhiteSpace(dto.SubscriptionId))
            {
                await EnsureSubscriptionExistsAsync(tenantId.Value, dto, ct);
            }

            await ApplyCreditTopUpIfNeededAsync(tenantId.Value, dto, ct);

            await AppendBillingLedgerAsync(tenantId.Value, dto, 0m, "checkout.session.completed", ct);
        }

        private async Task HandleSubscriptionChangedAsync(PaymentWebhookEventDto dto, CancellationToken ct)
        {
            var tenantId = await ResolveTenantIdAsync(dto, ct);
            if (!tenantId.HasValue)
                return;

            if (!string.IsNullOrWhiteSpace(dto.CustomerId))
            {
                var partitionKey = EntityKeyPolicy.TenantPartition(tenantId.Value);
                var billingAccount = await _billingAccountRepository.GetAsync(partitionKey, "BILLING", ct);
                if (billingAccount is not null)
                {
                    billingAccount.ProviderCustomerId = dto.CustomerId;
                    billingAccount.BillingStatus = MapBillingStatus(dto.EventType, dto.Status);
                    billingAccount.DateUpdated = DateTime.UtcNow;
                    await _billingAccountRepository.SaveAsync(billingAccount, ct);
                }
            }

            await UpsertSubscriptionAsync(tenantId.Value, dto, ct);
            await UpsertSeatEntitlementAsync(tenantId.Value, dto, ct);
            await AppendBillingLedgerAsync(tenantId.Value, dto, 0m, dto.EventType, ct);
        }

        private async Task HandleInvoiceStatusAsync(PaymentWebhookEventDto dto, CancellationToken ct)
        {
            var tenantId = await ResolveTenantIdAsync(dto, ct);
            if (!tenantId.HasValue)
                return;

            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId.Value);
            var billingAccount = await _billingAccountRepository.GetAsync(partitionKey, "BILLING", ct);
            if (billingAccount is not null)
            {
                billingAccount.BillingStatus = MapBillingStatus(dto.EventType, dto.Status);
                billingAccount.DateUpdated = DateTime.UtcNow;
                await _billingAccountRepository.SaveAsync(billingAccount, ct);
            }

            if (!string.IsNullOrWhiteSpace(dto.SubscriptionId))
            {
                var subscription = await _subscriptionRepository.GetByProviderSubscriptionIdAsync(dto.Provider, dto.SubscriptionId, ct);
                if (subscription is null)
                {
                    await UpsertSubscriptionAsync(tenantId.Value, dto, ct);
                    await UpsertSeatEntitlementAsync(tenantId.Value, dto, ct);
                }
                else
                {
                    var updatedStatus = MapSubscriptionStatus(dto.EventType, dto.Status);
                    if (!string.IsNullOrWhiteSpace(updatedStatus))
                        subscription.SubscriptionStatus = updatedStatus;
                    subscription.CurrentSeatCount = dto.SeatCount ?? subscription.CurrentSeatCount;
                    subscription.NextRenewalSeatCount = dto.SeatCount ?? subscription.NextRenewalSeatCount;
                    subscription.TermStartUtc = dto.CurrentPeriodStartUtc ?? subscription.TermStartUtc;
                    subscription.TermEndUtc = dto.CurrentPeriodEndUtc ?? subscription.TermEndUtc;
                    subscription.DateUpdated = DateTime.UtcNow;
                    await _subscriptionRepository.SaveAsync(subscription, ct);
                    await UpsertSeatEntitlementAsync(tenantId.Value, dto, ct);
                }
            }

            var amount = ExtractInvoiceAmount(dto.PayloadJson);
            await AppendBillingLedgerAsync(tenantId.Value, dto, amount, dto.EventType, ct);
            if (string.Equals(dto.EventType, "invoice.payment_failed", StringComparison.OrdinalIgnoreCase))
            {
                await _alertService.RaiseAsync(new RaiseOperationalAlertRequestDto
                {
                    TenantId = tenantId.Value,
                    AlertType = "payment_failure",
                    Severity = "error",
                    DedupeKey = $"payment-failed:{dto.SubscriptionId ?? dto.CustomerId ?? dto.EventId}",
                    Source = nameof(BillingEventService),
                    Message = $"Payment failed for subscription '{dto.SubscriptionId ?? "unknown"}'.",
                    ContextJson = dto.PayloadJson
                }, ct);
            }
            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                TenantId = tenantId.Value,
                Category = "billing",
                Action = dto.EventType.Replace('.', '_'),
                Severity = string.Equals(dto.EventType, "invoice.payment_failed", StringComparison.OrdinalIgnoreCase) ? "warning" : "info",
                TargetType = "subscription",
                TargetId = dto.SubscriptionId,
                Source = nameof(BillingEventService),
                Description = $"Handled billing provider event {dto.EventType}."
            }, ct);
        }

        private async Task UpsertSubscriptionAsync(Guid tenantId, PaymentWebhookEventDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.SubscriptionId))
                return;

            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var existing = await _subscriptionRepository.GetByProviderSubscriptionIdAsync(dto.Provider, dto.SubscriptionId, ct);
            var entity = existing ?? new TenantSubscription
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartitionKey = partitionKey,
                RowKey = EntityKeyPolicy.Row(Guid.NewGuid()),
                Provider = dto.Provider,
                ProviderSubscriptionId = dto.SubscriptionId,
                DateCreated = DateTime.UtcNow,
                IsDeleted = false
            };

            if (existing is null)
            {
                entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            }

            entity.PlanCode = dto.PriceKey ?? entity.PlanCode;
            entity.BillingCadence = DeriveBillingCadence(dto.PriceKey) ?? entity.BillingCadence;
            var subscriptionStatus = MapSubscriptionStatus(dto.EventType, dto.Status);
            if (!string.IsNullOrWhiteSpace(subscriptionStatus))
                entity.SubscriptionStatus = subscriptionStatus;
            entity.CurrentSeatCount = dto.SeatCount ?? entity.CurrentSeatCount;
            entity.NextRenewalSeatCount = dto.SeatCount ?? entity.NextRenewalSeatCount;
            entity.CancelAtTermEnd = dto.CancelAtPeriodEnd ?? entity.CancelAtTermEnd;
            entity.TermStartUtc = dto.CurrentPeriodStartUtc ?? entity.TermStartUtc;
            entity.TermEndUtc = dto.CurrentPeriodEndUtc ?? entity.TermEndUtc;
            entity.DateUpdated = DateTime.UtcNow;

            await _subscriptionRepository.SaveAsync(entity, ct);
        }

        private async Task EnsureSubscriptionExistsAsync(Guid tenantId, PaymentWebhookEventDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.SubscriptionId))
                return;

            var existing = await _subscriptionRepository.GetByProviderSubscriptionIdAsync(dto.Provider, dto.SubscriptionId, ct);
            if (existing is not null)
                return;

            var partitionKey = EntityKeyPolicy.TenantPartition(tenantId);
            var entity = new TenantSubscription
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartitionKey = partitionKey,
                RowKey = EntityKeyPolicy.Row(Guid.NewGuid()),
                Provider = dto.Provider,
                ProviderSubscriptionId = dto.SubscriptionId,
                PlanCode = dto.PriceKey,
                BillingCadence = DeriveBillingCadence(dto.PriceKey),
                SubscriptionStatus = "pending",
                CurrentSeatCount = 0,
                NextRenewalSeatCount = 0,
                CancelAtTermEnd = false,
                TermStartUtc = DateTime.MinValue,
                TermEndUtc = DateTime.MinValue,
                DateCreated = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow,
                IsDeleted = false
            };

            entity.RowKey = EntityKeyPolicy.Row(entity.Id);
            await _subscriptionRepository.SaveAsync(entity, ct);
        }

        private async Task UpsertSeatEntitlementAsync(Guid tenantId, PaymentWebhookEventDto dto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(dto.SubscriptionId))
                return;

            var subscription = await _subscriptionRepository.GetByProviderSubscriptionIdAsync(dto.Provider, dto.SubscriptionId, ct);
            if (subscription is null)
                return;

            var purchasedSeats = dto.SeatCount ?? subscription.CurrentSeatCount;
            await _seatEntitlementService.SyncPurchasedSeatsAsync(tenantId, subscription.Id, purchasedSeats, ct);
        }

        private async Task AppendBillingLedgerAsync(Guid tenantId, PaymentWebhookEventDto dto, decimal amount, string description, CancellationToken ct)
        {
            var entity = new BillingLedger
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartitionKey = EntityKeyPolicy.TenantPartition(tenantId),
                RowKey = RepositoryKeyHelper.ToOrderedRowKey(Guid.NewGuid()),
                Provider = dto.Provider,
                EventType = dto.EventType,
                ProviderEventId = dto.EventId,
                ProviderSubscriptionId = dto.SubscriptionId,
                Amount = amount,
                Currency = "USD",
                Description = description,
                EffectiveUtc = DateTime.UtcNow,
                IsDeleted = false
            };

            await _billingLedgerRepository.SaveAsync(entity, ct);
        }

        private async Task ApplyCreditTopUpIfNeededAsync(Guid tenantId, PaymentWebhookEventDto dto, CancellationToken ct)
        {
            if (!string.Equals(dto.PurchaseType, "credit_topup", StringComparison.OrdinalIgnoreCase))
                return;
            if (string.IsNullOrWhiteSpace(dto.PriceKey))
                return;

            var provider = _paymentProviderResolver.GetRequiredProvider(dto.Provider);
            if (!provider.TryGetTopUpCreditAmount(dto.PriceKey, out var creditsPerPack))
                return;

            var quantity = dto.Quantity.GetValueOrDefault(1);
            if (quantity < 1)
                quantity = 1;

            var (periodStartUtc, periodEndUtc) = await ResolveCreditWindowAsync(tenantId, ct);
            await _creditAccountingService.AddPurchasedCreditsAsync(
                tenantId,
                creditsPerPack * quantity,
                periodStartUtc,
                periodEndUtc,
                periodEndUtc,
                $"{dto.Provider.ToLowerInvariant()}:event:{dto.EventId}",
                $"{dto.Provider} credit top-up for {dto.PriceKey}.",
                DateTime.UtcNow,
                ct);
        }

        private async Task<(DateTime PeriodStartUtc, DateTime PeriodEndUtc)> ResolveCreditWindowAsync(Guid tenantId, CancellationToken ct)
        {
            var tenantPartition = EntityKeyPolicy.TenantPartition(tenantId);
            var subscription = await _subscriptionRepository.GetCurrentAsync(tenantPartition, ct);
            if (subscription is not null && subscription.TermEndUtc > subscription.TermStartUtc)
                return (subscription.TermStartUtc, subscription.TermEndUtc);

            var creditBalance = await _creditBalanceRepository.GetAsync(tenantPartition, "CREDITS", ct);
            if (creditBalance is not null && creditBalance.CurrentUsagePeriodEndUtc > creditBalance.CurrentUsagePeriodStartUtc)
            {
                return (creditBalance.CurrentUsagePeriodStartUtc, creditBalance.CurrentUsagePeriodEndUtc);
            }

            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddMonths(1));
        }

        private async Task<Guid?> ResolveTenantIdAsync(PaymentWebhookEventDto dto, CancellationToken ct)
        {
            if (dto.TenantId.HasValue && dto.TenantId.Value != Guid.Empty)
                return dto.TenantId.Value;

            if (!string.IsNullOrWhiteSpace(dto.SubscriptionId))
            {
                var subscription = await _subscriptionRepository.GetByProviderSubscriptionIdAsync(dto.Provider, dto.SubscriptionId, ct);
                if (subscription is not null)
                    return subscription.TenantId;
            }

            if (!string.IsNullOrWhiteSpace(dto.CustomerId))
            {
                var billingAccount = await _billingAccountRepository.GetByProviderCustomerIdAsync(dto.Provider, dto.CustomerId, ct);
                if (billingAccount is not null)
                    return billingAccount.TenantId;
            }

            return null;
        }

        private static string MapBillingStatus(string eventType, string? status) =>
            eventType switch
            {
                "checkout.session.completed" => "active",
                "invoice.payment_failed" => "past_due",
                "invoice.paid" => "active",
                _ => string.IsNullOrWhiteSpace(status) ? "active" : status.Trim()
            };

        private static string? MapSubscriptionStatus(string eventType, string? status) =>
            eventType switch
            {
                "customer.subscription.deleted" => "canceled",
                "customer.subscription.created" => NormalizeSubscriptionStatus(status) ?? "active",
                "customer.subscription.updated" => NormalizeSubscriptionStatus(status) ?? "active",
                "invoice.payment_failed" => "past_due",
                "invoice.paid" => "active",
                "checkout.session.completed" => null,
                _ => NormalizeSubscriptionStatus(status)
            };

        private static string? NormalizeSubscriptionStatus(string? status)
            => string.IsNullOrWhiteSpace(status) ? null : status.Trim();

        private static string? DeriveBillingCadence(string? priceKey)
        {
            if (string.IsNullOrWhiteSpace(priceKey))
                return null;

            if (priceKey.Contains("annual", StringComparison.OrdinalIgnoreCase))
                return "annual";
            if (priceKey.Contains("quarter", StringComparison.OrdinalIgnoreCase))
                return "quarterly";
            if (priceKey.Contains("month", StringComparison.OrdinalIgnoreCase))
                return "monthly";

            return null;
        }

        private static decimal ExtractInvoiceAmount(string payloadJson)
        {
            using var document = JsonDocument.Parse(payloadJson);
            if (document.RootElement.TryGetProperty("data", out var data)
                && data.TryGetProperty("object", out var obj)
                && obj.TryGetProperty("amount_paid", out var amountPaid)
                && amountPaid.TryGetInt64(out var cents))
            {
                return cents / 100m;
            }

            if (document.RootElement.TryGetProperty("data", out data)
                && data.TryGetProperty("object", out obj)
                && obj.TryGetProperty("amount_due", out var amountDue)
                && amountDue.TryGetInt64(out cents))
            {
                return cents / 100m;
            }

            return 0m;
        }
    }
}
