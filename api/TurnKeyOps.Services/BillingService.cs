using MedInsights.Lib.Dtos;
using MedInsights.Lib;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class BillingService : IBillingService
    {
        private readonly IPaymentProviderResolver _paymentProviderResolver;
        private readonly ITenantSeatEntitlementService _seatEntitlementService;
        private readonly ITenantSubscriptionRepository _subscriptionRepository;
        private readonly ITenantBillingAccountRepository _billingAccountRepository;
        private readonly IUserContext _userContext;
        private readonly IAuditService _auditService;
        private readonly ITenantMembershipAuthorizationService _membershipAuthorizationService;

        public BillingService(
            IPaymentProviderResolver paymentProviderResolver,
            ITenantSeatEntitlementService seatEntitlementService,
            ITenantSubscriptionRepository subscriptionRepository,
            ITenantBillingAccountRepository billingAccountRepository,
            IUserContext userContext,
            IAuditService auditService,
            ITenantMembershipAuthorizationService membershipAuthorizationService)
        {
            _paymentProviderResolver = paymentProviderResolver;
            _seatEntitlementService = seatEntitlementService;
            _subscriptionRepository = subscriptionRepository;
            _billingAccountRepository = billingAccountRepository;
            _userContext = userContext;
            _auditService = auditService;
            _membershipAuthorizationService = membershipAuthorizationService;
        }

        public async Task<PaymentCheckoutSessionDto> CreateSubscriptionCheckoutAsync(CreateSubscriptionCheckoutRequestDto dto, CancellationToken ct = default)
        {
            await _membershipAuthorizationService.RequireBillingAccessAsync(ct);
            Enrich(dto);
            var provider = await _paymentProviderResolver.ResolveForTenantAsync(dto.TenantId, dto.Provider, ct);
            dto.Provider = provider.ProviderName;
            var result = await provider.CreateSubscriptionCheckoutAsync(dto, ct);
            await AuditAsync("billing", "subscription_checkout_requested", $"Requested subscription checkout for {dto.PriceKey}.", dto.PriceKey, ct);
            return result;
        }

        public async Task<PaymentPortalSessionDto> CreateCustomerPortalAsync(CreateCustomerPortalRequestDto dto, CancellationToken ct = default)
        {
            await _membershipAuthorizationService.RequireBillingAccessAsync(ct);
            Enrich(dto);
            var provider = await _paymentProviderResolver.ResolveForCustomerAsync(dto.TenantId, dto.CustomerId, dto.Provider, ct);
            dto.Provider = provider.ProviderName;
            var result = await provider.CreateCustomerPortalAsync(dto, ct);
            await AuditAsync("billing", "customer_portal_requested", "Requested billing portal session.", dto.CustomerId, ct);
            return result;
        }

        public Task<PaymentSubscriptionResultDto> UpdateSubscriptionSeatsAsync(UpdateSubscriptionSeatsRequestDto dto, CancellationToken ct = default)
            => UpdateSeatsInternalAsync(dto, ct);

        public Task<PaymentSubscriptionResultDto> ScheduleSeatReductionAsync(ScheduleSeatReductionRequestDto dto, CancellationToken ct = default)
            => ScheduleSeatReductionInternalAsync(dto, ct);

        public async Task<PaymentCheckoutSessionDto> PurchaseCreditTopUpAsync(PurchaseCreditTopUpRequestDto dto, CancellationToken ct = default)
        {
            await _membershipAuthorizationService.RequireBillingAccessAsync(ct);
            Enrich(dto);
            var provider = await _paymentProviderResolver.ResolveForTenantAsync(dto.TenantId, dto.Provider, ct);
            dto.Provider = provider.ProviderName;
            var result = await provider.PurchaseCreditTopUpAsync(dto, ct);
            await AuditAsync("billing", "credit_topup_checkout_requested", $"Requested credit top-up checkout for {dto.PriceKey}.", dto.PriceKey, ct);
            return result;
        }

        public async Task<PaymentSubscriptionResultDto> CancelAtTermEndAsync(CancelSubscriptionRequestDto dto, CancellationToken ct = default)
        {
            await _membershipAuthorizationService.RequireBillingAccessAsync(ct);
            var provider = await _paymentProviderResolver.ResolveForSubscriptionAsync(dto.SubscriptionId, dto.Provider, ct);
            dto.Provider = provider.ProviderName;
            var result = await provider.CancelAtTermEndAsync(dto, ct);
            await AuditAsync("billing", "subscription_cancel_requested", "Requested cancel at term end.", dto.SubscriptionId, ct);
            return result;
        }

        public async Task<PaymentSubscriptionResultDto> ReactivateAsync(ReactivateSubscriptionRequestDto dto, CancellationToken ct = default)
        {
            await _membershipAuthorizationService.RequireBillingAccessAsync(ct);
            var provider = await _paymentProviderResolver.ResolveForSubscriptionAsync(dto.SubscriptionId, dto.Provider, ct);
            dto.Provider = provider.ProviderName;
            var result = await provider.ReactivateAsync(dto, ct);
            await AuditAsync("billing", "subscription_reactivate_requested", "Requested subscription reactivation.", dto.SubscriptionId, ct);
            return result;
        }

        private void Enrich(CreateSubscriptionCheckoutRequestDto dto)
        {
            if (_userContext.IsAuthenticated)
            {
                dto.TenantId ??= _userContext.TenantId;
                dto.RequestedByUserId ??= _userContext.UserId;
            }
        }

        private void Enrich(PurchaseCreditTopUpRequestDto dto)
        {
            if (_userContext.IsAuthenticated)
            {
                dto.TenantId ??= _userContext.TenantId;
                dto.RequestedByUserId ??= _userContext.UserId;
            }
        }

        private void Enrich(CreateCustomerPortalRequestDto dto)
        {
            if (_userContext.IsAuthenticated)
                dto.TenantId ??= _userContext.TenantId;
        }

        private async Task<PaymentSubscriptionResultDto> UpdateSeatsInternalAsync(UpdateSubscriptionSeatsRequestDto dto, CancellationToken ct)
        {
            await _membershipAuthorizationService.RequireBillingAccessAsync(ct);
            var entitlement = await _seatEntitlementService.GetCurrentAsync(ct)
                ?? throw new InvalidOperationException("Tenant seat entitlement was not found.");

            if (dto.SeatCount < entitlement.PurchasedSeats)
            {
                var scheduled = await _seatEntitlementService.ScheduleSeatReductionAsync(_userContext.TenantId, dto.SeatCount, ct);
                var subscription = await _subscriptionRepository.GetCurrentAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), ct);

                return new PaymentSubscriptionResultDto
                {
                    Provider = subscription?.Provider ?? dto.Provider ?? _paymentProviderResolver.GetDefaultProvider().ProviderName,
                    SubscriptionId = dto.SubscriptionId,
                    Status = subscription?.SubscriptionStatus ?? "active",
                    SeatCount = entitlement.PurchasedSeats,
                    NextRenewalSeatCount = scheduled.NextRenewalSeatCount,
                    CancelAtPeriodEnd = subscription?.CancelAtTermEnd ?? false,
                    CurrentPeriodStartUtc = subscription?.TermStartUtc,
                    CurrentPeriodEndUtc = subscription?.TermEndUtc
                };
            }

            var provider = await _paymentProviderResolver.ResolveForSubscriptionAsync(dto.SubscriptionId, dto.Provider, ct);
            dto.Provider = provider.ProviderName;
            var result = await provider.UpdateSubscriptionSeatsAsync(dto, ct);
            await UpdateLocalSeatStateAsync(dto.SubscriptionId, provider.ProviderName, result.SeatCount, ct);
            result.NextRenewalSeatCount = result.SeatCount;
            await AuditAsync("billing", "subscription_seats_updated", $"Updated subscription seats to {result.SeatCount}.", dto.SubscriptionId, ct);
            return result;
        }

        private async Task<PaymentSubscriptionResultDto> ScheduleSeatReductionInternalAsync(ScheduleSeatReductionRequestDto dto, CancellationToken ct)
        {
            await _membershipAuthorizationService.RequireBillingAccessAsync(ct);
            if (dto.SeatCount < 0)
                throw new ArgumentOutOfRangeException(nameof(dto.SeatCount));

            var entitlement = await _seatEntitlementService.ScheduleSeatReductionAsync(_userContext.TenantId, dto.SeatCount, ct);
            var subscription = await _subscriptionRepository.GetCurrentAsync(EntityKeyPolicy.TenantPartition(_userContext.TenantId), ct);

            return new PaymentSubscriptionResultDto
            {
                Provider = subscription?.Provider ?? _paymentProviderResolver.GetDefaultProvider().ProviderName,
                SubscriptionId = dto.SubscriptionId,
                Status = subscription?.SubscriptionStatus ?? "active",
                SeatCount = entitlement.PurchasedSeats,
                NextRenewalSeatCount = entitlement.NextRenewalSeatCount,
                CancelAtPeriodEnd = subscription?.CancelAtTermEnd ?? false,
                CurrentPeriodStartUtc = subscription?.TermStartUtc,
                CurrentPeriodEndUtc = subscription?.TermEndUtc
            };
        }

        private Task AuditAsync(string category, string action, string description, string? targetId, CancellationToken ct)
            => _auditService.RecordAsync(new MedInsights.Lib.Dtos.RecordAuditEventRequestDto
            {
                Category = category,
                Action = action,
                Severity = "info",
                TargetType = "billing",
                TargetId = targetId,
                Source = nameof(BillingService),
                Description = description
            }, ct);

        private async Task UpdateLocalSeatStateAsync(string subscriptionId, string providerName, int seatCount, CancellationToken ct)
        {
            var subscription = await _subscriptionRepository.GetByProviderSubscriptionIdAsync(providerName, subscriptionId, ct);
            if (subscription is null)
                return;

            subscription.CurrentSeatCount = seatCount;
            subscription.NextRenewalSeatCount = seatCount;
            subscription.DateUpdated = DateTime.UtcNow;
            await _subscriptionRepository.SaveAsync(subscription, ct);
            await _seatEntitlementService.SyncPurchasedSeatsAsync(subscription.TenantId, subscription.Id, seatCount, ct);
        }
    }
}
