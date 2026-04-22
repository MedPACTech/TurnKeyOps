using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class CreditAccountingService : ICreditAccountingService
    {
        private const string BalanceRowKey = "CREDITS";
        private const string BillingRowKey = "BILLING";
        private const string LedgerGrantType = "grant";
        private const string LedgerConsumeType = "consume";
        private const string LedgerExpireType = "expire";
        private const string IncludedBucket = "included";
        private const string PurchasedBucket = "purchased";
        private static readonly TimeSpan AutoTopUpAttemptCooldown = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan AutoTopUpFailureCooldown = TimeSpan.FromMinutes(15);

        private readonly ITenantCreditBalanceRepository _tenantBalanceRepository;
        private readonly ITenantBillingAccountRepository _tenantBillingAccountRepository;
        private readonly IUserCreditPeriodRepository _userCreditPeriodRepository;
        private readonly ICreditLedgerRepository _creditLedgerRepository;
        private readonly IBillingLedgerRepository _billingLedgerRepository;
        private readonly IPaymentProviderResolver _paymentProviderResolver;
        private readonly ICreditUsageDispatchService _creditUsageDispatchService;
        private readonly IOperationalAlertService _alertService;
        private readonly IAuditService _auditService;

        public CreditAccountingService(
            ITenantCreditBalanceRepository tenantBalanceRepository,
            ITenantBillingAccountRepository tenantBillingAccountRepository,
            IUserCreditPeriodRepository userCreditPeriodRepository,
            ICreditLedgerRepository creditLedgerRepository,
            IBillingLedgerRepository billingLedgerRepository,
            IPaymentProviderResolver paymentProviderResolver,
            ICreditUsageDispatchService creditUsageDispatchService,
            IOperationalAlertService alertService,
            IAuditService auditService)
        {
            _tenantBalanceRepository = tenantBalanceRepository;
            _tenantBillingAccountRepository = tenantBillingAccountRepository;
            _userCreditPeriodRepository = userCreditPeriodRepository;
            _creditLedgerRepository = creditLedgerRepository;
            _billingLedgerRepository = billingLedgerRepository;
            _paymentProviderResolver = paymentProviderResolver;
            _creditUsageDispatchService = creditUsageDispatchService;
            _alertService = alertService;
            _auditService = auditService;
        }

        public async Task<TenantCreditBalanceDto> EnsureTenantBalanceAsync(
            Guid tenantId,
            DateTime usagePeriodStartUtc,
            DateTime usagePeriodEndUtc,
            bool? softCapAlertEnabled = null,
            CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));
            if (usagePeriodEndUtc <= usagePeriodStartUtc)
                throw new ArgumentOutOfRangeException(nameof(usagePeriodEndUtc), "Usage period end must be after start.");

            var entity = await GetOrCreateTenantBalanceAsync(tenantId, usagePeriodStartUtc, usagePeriodEndUtc, softCapAlertEnabled, ct);
            return TenantCreditBalanceMapper.ToDto(entity);
        }

        public async Task<UserCreditPeriodDto> GrantIncludedCreditsAsync(
            Guid tenantId,
            Guid userId,
            string usagePeriodKey,
            int credits,
            int? softCapThreshold = null,
            string? sourceReference = null,
            string? description = null,
            DateTime? effectiveUtc = null,
            CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId is required.", nameof(userId));
            if (credits <= 0)
                throw new ArgumentOutOfRangeException(nameof(credits), "Credits must be greater than zero.");

            var normalizedPeriodKey = NormalizePeriodKey(usagePeriodKey);
            var period = await GetOrCreateUserPeriodAsync(tenantId, userId, normalizedPeriodKey, ct);

            period.IncludedCreditsGranted += credits;
            if (softCapThreshold.HasValue)
                period.SoftCapThreshold = softCapThreshold.Value;
            period.DateUpdated = DateTime.UtcNow;

            period = await _userCreditPeriodRepository.SaveAsync(period, ct);

            await AppendCreditLedgerEntryAsync(
                tenantId,
                userId,
                LedgerGrantType,
                IncludedBucket,
                credits,
                Math.Max(0, period.IncludedCreditsGranted - period.IncludedCreditsConsumed),
                normalizedPeriodKey,
                null,
                sourceReference,
                description ?? "Granted included monthly credits.",
                effectiveUtc ?? DateTime.UtcNow,
                ct);

            return UserCreditPeriodMapper.ToDto(period);
        }

        public async Task<TenantCreditBalanceDto> AddPurchasedCreditsAsync(
            Guid tenantId,
            int credits,
            DateTime usagePeriodStartUtc,
            DateTime usagePeriodEndUtc,
            DateTime expiresAtUtc,
            string? sourceReference = null,
            string? description = null,
            DateTime? effectiveUtc = null,
            CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));
            if (credits <= 0)
                throw new ArgumentOutOfRangeException(nameof(credits), "Credits must be greater than zero.");
            if (usagePeriodEndUtc <= usagePeriodStartUtc)
                throw new ArgumentOutOfRangeException(nameof(usagePeriodEndUtc), "Usage period end must be after start.");

            var appliedUtc = effectiveUtc ?? DateTime.UtcNow;
            var balance = await GetOrCreateTenantBalanceAsync(tenantId, usagePeriodStartUtc, usagePeriodEndUtc, null, ct);

            balance.PurchasedCreditsAvailable += credits;
            balance.PurchasedCreditsExpireAtUtc = balance.PurchasedCreditsExpireAtUtc == default
                ? expiresAtUtc
                : MaxUtc(balance.PurchasedCreditsExpireAtUtc, expiresAtUtc);
            balance.LastTopUpUtc = appliedUtc;
            balance.TopUpsThisCycle += 1;
            balance.DateUpdated = DateTime.UtcNow;

            balance = await _tenantBalanceRepository.SaveAsync(balance, ct);

            await AppendCreditLedgerEntryAsync(
                tenantId,
                null,
                LedgerGrantType,
                PurchasedBucket,
                credits,
                balance.PurchasedCreditsAvailable,
                null,
                balance.PurchasedCreditsExpireAtUtc,
                sourceReference,
                description ?? "Purchased top-up credits.",
                appliedUtc,
                ct);

            return TenantCreditBalanceMapper.ToDto(balance);
        }

        public async Task<CreditConsumptionResultDto> ConsumeCreditsAsync(
            Guid tenantId,
            Guid userId,
            string usagePeriodKey,
            int credits,
            string? sourceReference = null,
            string? description = null,
            DateTime? effectiveUtc = null,
            CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId is required.", nameof(userId));
            if (credits <= 0)
                throw new ArgumentOutOfRangeException(nameof(credits), "Credits must be greater than zero.");

            var normalizedPeriodKey = NormalizePeriodKey(usagePeriodKey);
            var appliedUtc = effectiveUtc ?? DateTime.UtcNow;
            var requestId = await _creditUsageDispatchService.EnqueueAsync(new CreditUsageMessageDto
            {
                TenantId = tenantId,
                UserId = userId,
                UsagePeriodKey = normalizedPeriodKey,
                Credits = credits,
                SourceReference = sourceReference,
                Description = description,
                EffectiveUtc = appliedUtc
            }, ct);

            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                TenantId = tenantId,
                UserId = userId,
                Category = "billing",
                Action = "credit_usage_queued",
                Severity = "info",
                TargetType = "credit_usage_request",
                TargetId = requestId.ToString("D"),
                Source = nameof(CreditAccountingService),
                Description = $"Queued credit usage request for {credits} credits in period '{normalizedPeriodKey}'."
            }, ct);

            return new CreditConsumptionResultDto
            {
                TenantId = tenantId,
                UserId = userId,
                UsagePeriodKey = normalizedPeriodKey,
                RequestedCredits = credits,
                EffectiveUtc = appliedUtc
            };
        }

        public async Task<CreditConsumptionResultDto> ConsumeCreditsDirectAsync(
            Guid tenantId,
            Guid userId,
            string usagePeriodKey,
            int credits,
            string? sourceReference = null,
            string? description = null,
            DateTime? effectiveUtc = null,
            CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));
            if (userId == Guid.Empty)
                throw new ArgumentException("UserId is required.", nameof(userId));
            if (credits <= 0)
                throw new ArgumentOutOfRangeException(nameof(credits), "Credits must be greater than zero.");

            var appliedUtc = effectiveUtc ?? DateTime.UtcNow;
            var normalizedPeriodKey = NormalizePeriodKey(usagePeriodKey);
            var period = await GetOrCreateUserPeriodAsync(tenantId, userId, normalizedPeriodKey, ct);
            var balance = await _tenantBalanceRepository.GetAsync(TenantPartition(tenantId), BalanceRowKey, ct);

            balance = await ExpirePurchasedCreditsIfNeededAsync(tenantId, balance, appliedUtc, sourceReference, ct);

            var includedAvailable = Math.Max(0, period.IncludedCreditsGranted - period.IncludedCreditsConsumed);
            var purchasedAvailable = Math.Max(0, balance?.PurchasedCreditsAvailable ?? 0);

            var includedToConsume = Math.Min(credits, includedAvailable);
            var remaining = credits - includedToConsume;
            var purchasedToConsume = Math.Min(remaining, purchasedAvailable);
            remaining -= purchasedToConsume;

            if (remaining > 0)
                throw new InvalidOperationException("Insufficient credits are available.");

            if (includedToConsume > 0)
                period.IncludedCreditsConsumed += includedToConsume;
            if (purchasedToConsume > 0)
                period.PurchasedCreditsConsumed += purchasedToConsume;
            period.DateUpdated = DateTime.UtcNow;
            period = await _userCreditPeriodRepository.SaveAsync(period, ct);

            if (purchasedToConsume > 0 && balance is not null)
            {
                balance.PurchasedCreditsAvailable -= purchasedToConsume;
                balance.DateUpdated = DateTime.UtcNow;
                balance = await _tenantBalanceRepository.SaveAsync(balance, ct);
            }

            var descriptionText = description ?? "Consumed credits.";
            if (includedToConsume > 0)
            {
                await AppendCreditLedgerEntryAsync(
                    tenantId,
                    userId,
                    LedgerConsumeType,
                    IncludedBucket,
                    -includedToConsume,
                    Math.Max(0, period.IncludedCreditsGranted - period.IncludedCreditsConsumed),
                    normalizedPeriodKey,
                    null,
                    BuildSourceReference(sourceReference, IncludedBucket),
                    descriptionText,
                    appliedUtc,
                    ct);
            }

            if (purchasedToConsume > 0)
            {
                await AppendCreditLedgerEntryAsync(
                    tenantId,
                    userId,
                    LedgerConsumeType,
                    PurchasedBucket,
                    -purchasedToConsume,
                    balance?.PurchasedCreditsAvailable ?? 0,
                    normalizedPeriodKey,
                    balance?.PurchasedCreditsExpireAtUtc,
                    BuildSourceReference(sourceReference, PurchasedBucket),
                    descriptionText,
                    appliedUtc,
                    ct);
            }

            await EvaluateSoftCapAlertAsync(
                tenantId,
                userId,
                period,
                balance,
                purchasedToConsume,
                sourceReference,
                appliedUtc,
                ct);

            await EvaluateAutoTopUpAsync(
                tenantId,
                userId,
                sourceReference,
                "Automatic top-up evaluation after credit consumption.",
                appliedUtc,
                ct);

            return new CreditConsumptionResultDto
            {
                TenantId = tenantId,
                UserId = userId,
                UsagePeriodKey = normalizedPeriodKey,
                RequestedCredits = credits,
                IncludedCreditsConsumed = includedToConsume,
                PurchasedCreditsConsumed = purchasedToConsume,
                IncludedCreditsRemaining = Math.Max(0, period.IncludedCreditsGranted - period.IncludedCreditsConsumed),
                PurchasedCreditsRemaining = Math.Max(0, balance?.PurchasedCreditsAvailable ?? 0),
                EffectiveUtc = appliedUtc
            };
        }

        public async Task<bool> EvaluateAutoTopUpAsync(
            Guid tenantId,
            Guid? requestedByUserId = null,
            string? sourceReference = null,
            string? description = null,
            DateTime? effectiveUtc = null,
            CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));

            var appliedUtc = effectiveUtc ?? DateTime.UtcNow;
            var balance = await _tenantBalanceRepository.GetAsync(TenantPartition(tenantId), BalanceRowKey, ct);
            if (balance is null)
                return false;

            balance = await ExpirePurchasedCreditsIfNeededAsync(tenantId, balance, appliedUtc, sourceReference, ct);
            if (balance is null)
                return false;

            var purchasedAvailable = Math.Max(0, balance?.PurchasedCreditsAvailable ?? 0);

            var account = await _tenantBillingAccountRepository.GetAsync(TenantPartition(tenantId), BillingRowKey, ct);
            if (!CanAttemptAutoTopUp(account, balance.TopUpsThisCycle, purchasedAvailable, appliedUtc, out var guardrailReason))
            {
                if (!string.IsNullOrWhiteSpace(guardrailReason))
                {
                    await AppendBillingLedgerEntryAsync(
                        tenantId,
                        account?.Provider ?? _paymentProviderResolver.GetDefaultProvider().ProviderName,
                        "auto_topup.skipped",
                        null,
                        null,
                        0m,
                        "USD",
                        guardrailReason,
                        appliedUtc,
                        ct);
                }

                return false;
            }

            var priceKey = account!.TopUpPackSku!;
            var provider = _paymentProviderResolver.GetRequiredProvider(account.Provider);
            if (!provider.TryGetTopUpPriceAmount(priceKey, out var priceAmount))
                throw new InvalidOperationException($"No configured top-up amount for '{priceKey}'.");
            if (!provider.TryGetTopUpCreditAmount(priceKey, out var creditAmount))
                throw new InvalidOperationException($"No configured top-up credit amount for '{priceKey}'.");

            account.LastAutoTopUpAttemptUtc = appliedUtc;
            account.LastAutoTopUpError = null;
            account.DateUpdated = DateTime.UtcNow;
            account = await _tenantBillingAccountRepository.SaveAsync(account, ct);

            var paymentResult = await provider.PurchaseCreditTopUpAutomaticallyAsync(new AutoTopUpChargeRequestDto
            {
                Provider = provider.ProviderName,
                TenantId = tenantId,
                RequestedByUserId = requestedByUserId,
                CustomerId = account.ProviderCustomerId!,
                PaymentMethodId = account.DefaultPaymentMethodRef!,
                PriceKey = priceKey,
                Quantity = 1
            }, ct);

            if (paymentResult.Success)
            {
                account.LastAutoTopUpSuccessUtc = appliedUtc;
                account.LastAutoTopUpFailureUtc = null;
                account.AutoTopUpFailureCount = 0;
                account.LastAutoTopUpError = null;
                account.DateUpdated = DateTime.UtcNow;
                await _tenantBillingAccountRepository.SaveAsync(account, ct);

                await AddPurchasedCreditsAsync(
                    tenantId,
                    creditAmount,
                    balance!.CurrentUsagePeriodStartUtc,
                    balance.CurrentUsagePeriodEndUtc,
                    balance.CurrentUsagePeriodEndUtc,
                    sourceReference ?? $"auto-topup:{paymentResult.PaymentIntentId}",
                    description ?? $"Automatic top-up for {priceKey}.",
                    appliedUtc,
                    ct);

                await AppendBillingLedgerEntryAsync(
                    tenantId,
                    paymentResult.Provider,
                    "auto_topup.succeeded",
                    paymentResult.InvoiceId,
                    paymentResult.PaymentIntentId,
                    paymentResult.Amount,
                    paymentResult.Currency,
                    $"Automatic top-up succeeded for {priceKey}.",
                    appliedUtc,
                    ct);
                await _auditService.RecordAsync(new RecordAuditEventRequestDto
                {
                    TenantId = tenantId,
                    UserId = requestedByUserId,
                    Category = "billing",
                    Action = "auto_topup_succeeded",
                    Severity = "info",
                    TargetType = "payment_intent",
                    TargetId = paymentResult.PaymentIntentId,
                    Source = nameof(CreditAccountingService),
                    Description = $"Automatic top-up succeeded for {priceKey}."
                }, ct);

                return true;
            }

            account.LastAutoTopUpFailureUtc = appliedUtc;
            account.AutoTopUpFailureCount += 1;
            account.LastAutoTopUpError = paymentResult.ErrorMessage ?? paymentResult.ErrorCode;
            account.DateUpdated = DateTime.UtcNow;
            await _tenantBillingAccountRepository.SaveAsync(account, ct);

            await AppendBillingLedgerEntryAsync(
                tenantId,
                paymentResult.Provider,
                "auto_topup.failed",
                paymentResult.InvoiceId,
                paymentResult.PaymentIntentId,
                paymentResult.Amount,
                paymentResult.Currency,
                $"Automatic top-up failed for {priceKey}: {paymentResult.ErrorMessage ?? paymentResult.ErrorCode ?? "unknown error"}.",
                appliedUtc,
                ct);
            await _alertService.RaiseAsync(new RaiseOperationalAlertRequestDto
            {
                TenantId = tenantId,
                AlertType = "topup_failure",
                Severity = "error",
                DedupeKey = $"topup-failed:{tenantId:N}:{priceKey}",
                Source = nameof(CreditAccountingService),
                Message = $"Automatic top-up failed for {priceKey}.",
                ContextJson = paymentResult.ErrorMessage ?? paymentResult.ErrorCode
            }, ct);
            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                TenantId = tenantId,
                UserId = requestedByUserId,
                Category = "billing",
                Action = "auto_topup_failed",
                Severity = "warning",
                TargetType = "topup_pack",
                TargetId = priceKey,
                Source = nameof(CreditAccountingService),
                Description = $"Automatic top-up failed for {priceKey}."
            }, ct);

            return false;
        }

        private async Task EvaluateSoftCapAlertAsync(
            Guid tenantId,
            Guid userId,
            UserCreditPeriod period,
            TenantCreditBalance? balance,
            int purchasedCreditsConsumedThisRequest,
            string? sourceReference,
            DateTime effectiveUtc,
            CancellationToken ct)
        {
            if (purchasedCreditsConsumedThisRequest <= 0)
                return;
            if (balance is null || !balance.SoftCapAlertEnabled)
                return;
            if (!period.SoftCapThreshold.HasValue || period.SoftCapThreshold.Value <= 0)
                return;
            if (period.SoftCapAlertSentUtc.HasValue)
                return;
            if (period.PurchasedCreditsConsumed < period.SoftCapThreshold.Value)
                return;

            period.SoftCapAlertSentUtc = effectiveUtc;
            period.DateUpdated = DateTime.UtcNow;
            period = await _userCreditPeriodRepository.SaveAsync(period, ct);

            var alertMessage =
                $"User '{userId:D}' exceeded the purchased-credit soft cap for period '{period.PeriodKey}'.";
            var contextJson =
                $$"""
                {
                  "userId": "{{userId:D}}",
                  "periodKey": "{{period.PeriodKey}}",
                  "softCapThreshold": {{period.SoftCapThreshold.Value}},
                  "purchasedCreditsConsumed": {{period.PurchasedCreditsConsumed}},
                  "purchasedCreditsAvailableRemaining": {{Math.Max(0, balance.PurchasedCreditsAvailable)}},
                  "sourceReference": {{ToJsonStringLiteral(sourceReference)}}
                }
                """;

            await _alertService.RaiseAsync(new RaiseOperationalAlertRequestDto
            {
                TenantId = tenantId,
                AlertType = "soft_cap_warning",
                Severity = "warning",
                DedupeKey = $"soft-cap:{tenantId:N}:{userId:N}:{period.PeriodKey}",
                Source = nameof(CreditAccountingService),
                Message = alertMessage,
                ContextJson = contextJson
            }, ct);

            await _auditService.RecordAsync(new RecordAuditEventRequestDto
            {
                TenantId = tenantId,
                UserId = userId,
                Category = "billing",
                Action = "soft_cap_threshold_exceeded",
                Severity = "warning",
                TargetType = "user_credit_period",
                TargetId = $"{userId:D}:{period.PeriodKey}",
                Source = nameof(CreditAccountingService),
                Description = $"Purchased-credit soft cap exceeded for period '{period.PeriodKey}'."
            }, ct);
        }

        public async Task<TenantCreditBalanceDto?> ExpirePurchasedCreditsAsync(
            Guid tenantId,
            DateTime? effectiveUtc = null,
            string? sourceReference = null,
            string? description = null,
            CancellationToken ct = default)
        {
            if (tenantId == Guid.Empty)
                throw new ArgumentException("TenantId is required.", nameof(tenantId));

            var balance = await _tenantBalanceRepository.GetAsync(TenantPartition(tenantId), BalanceRowKey, ct);
            if (balance is null || balance.PurchasedCreditsAvailable <= 0)
                return balance is null ? null : TenantCreditBalanceMapper.ToDto(balance);

            var expiringCredits = balance.PurchasedCreditsAvailable;
            balance.PurchasedCreditsAvailable = 0;
            balance.DateUpdated = DateTime.UtcNow;
            balance = await _tenantBalanceRepository.SaveAsync(balance, ct);

            await AppendCreditLedgerEntryAsync(
                tenantId,
                null,
                LedgerExpireType,
                PurchasedBucket,
                -expiringCredits,
                0,
                null,
                balance.PurchasedCreditsExpireAtUtc,
                sourceReference,
                description ?? "Expired purchased credits at renewal.",
                effectiveUtc ?? DateTime.UtcNow,
                ct);

            return TenantCreditBalanceMapper.ToDto(balance);
        }

        private async Task<TenantCreditBalance> GetOrCreateTenantBalanceAsync(
            Guid tenantId,
            DateTime usagePeriodStartUtc,
            DateTime usagePeriodEndUtc,
            bool? softCapAlertEnabled,
            CancellationToken ct)
        {
            var partitionKey = TenantPartition(tenantId);
            var existing = await _tenantBalanceRepository.GetAsync(partitionKey, BalanceRowKey, ct);
            if (existing is null)
            {
                return await _tenantBalanceRepository.SaveAsync(new TenantCreditBalance
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    PartitionKey = partitionKey,
                    RowKey = BalanceRowKey,
                    CurrentUsagePeriodStartUtc = usagePeriodStartUtc,
                    CurrentUsagePeriodEndUtc = usagePeriodEndUtc,
                    PurchasedCreditsAvailable = 0,
                    PurchasedCreditsExpireAtUtc = usagePeriodEndUtc,
                    SoftCapAlertEnabled = softCapAlertEnabled ?? false,
                    LastTopUpUtc = null,
                    TopUpsThisCycle = 0,
                    DateCreated = DateTime.UtcNow,
                    DateUpdated = DateTime.UtcNow,
                    IsDeleted = false
                }, ct);
            }

            var dirty = false;
            var periodChanged =
                existing.CurrentUsagePeriodStartUtc != usagePeriodStartUtc ||
                existing.CurrentUsagePeriodEndUtc != usagePeriodEndUtc;

            if (periodChanged)
            {
                existing.CurrentUsagePeriodStartUtc = usagePeriodStartUtc;
                existing.CurrentUsagePeriodEndUtc = usagePeriodEndUtc;
                existing.TopUpsThisCycle = 0;
                existing.DateUpdated = DateTime.UtcNow;
                dirty = true;
            }

            if (softCapAlertEnabled.HasValue && existing.SoftCapAlertEnabled != softCapAlertEnabled.Value)
            {
                existing.SoftCapAlertEnabled = softCapAlertEnabled.Value;
                existing.DateUpdated = DateTime.UtcNow;
                dirty = true;
            }

            return dirty
                ? await _tenantBalanceRepository.SaveAsync(existing, ct)
                : existing;
        }

        private async Task<UserCreditPeriod> GetOrCreateUserPeriodAsync(Guid tenantId, Guid userId, string periodKey, CancellationToken ct)
        {
            var partitionKey = PeriodPartition(tenantId, periodKey);
            var rowKey = UserPeriodRow(userId);
            var existing = await _userCreditPeriodRepository.GetAsync(partitionKey, rowKey, ct);
            if (existing is not null)
                return existing;

            return await _userCreditPeriodRepository.SaveAsync(new UserCreditPeriod
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                PeriodKey = periodKey,
                IncludedCreditsGranted = 0,
                IncludedCreditsConsumed = 0,
                PurchasedCreditsConsumed = 0,
                SoftCapThreshold = null,
                SoftCapAlertSentUtc = null,
                PartitionKey = partitionKey,
                RowKey = rowKey,
                DateCreated = DateTime.UtcNow,
                DateUpdated = DateTime.UtcNow,
                IsDeleted = false
            }, ct);
        }

        private async Task<TenantCreditBalance?> ExpirePurchasedCreditsIfNeededAsync(
            Guid tenantId,
            TenantCreditBalance? balance,
            DateTime effectiveUtc,
            string? sourceReference,
            CancellationToken ct)
        {
            if (balance is null || balance.PurchasedCreditsAvailable <= 0)
                return balance;

            if (balance.PurchasedCreditsExpireAtUtc > effectiveUtc)
                return balance;

            return (await ExpirePurchasedCreditsAsync(
                tenantId,
                effectiveUtc,
                BuildSourceReference(sourceReference, "expiry"),
                "Expired purchased credits before consumption.",
                ct)) is not null
                ? await _tenantBalanceRepository.GetAsync(TenantPartition(tenantId), BalanceRowKey, ct)
                : null;
        }

        private async Task AppendCreditLedgerEntryAsync(
            Guid tenantId,
            Guid? userId,
            string ledgerType,
            string sourceBucket,
            int amount,
            int balanceAfter,
            string? usagePeriodKey,
            DateTime? expiresAtUtc,
            string? sourceReference,
            string description,
            DateTime effectiveUtc,
            CancellationToken ct)
        {
            await _creditLedgerRepository.SaveAsync(new CreditLedger
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UserId = userId,
                PartitionKey = TenantPartition(tenantId),
                RowKey = RepositoryKeyHelper.ToOrderedRowKey(Guid.NewGuid()),
                LedgerType = ledgerType,
                SourceBucket = sourceBucket,
                Amount = amount,
                BalanceAfter = balanceAfter,
                UsagePeriodKey = usagePeriodKey,
                ExpiresAtUtc = expiresAtUtc,
                SourceReference = sourceReference,
                Description = description,
                EffectiveUtc = effectiveUtc,
                IsDeleted = false
            }, ct);
        }

        private async Task AppendBillingLedgerEntryAsync(
            Guid tenantId,
            string provider,
            string eventType,
            string? providerInvoiceId,
            string? providerPaymentIntentId,
            decimal amount,
            string currency,
            string description,
            DateTime effectiveUtc,
            CancellationToken ct)
        {
            await _billingLedgerRepository.SaveAsync(new BillingLedger
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                PartitionKey = TenantPartition(tenantId),
                RowKey = RepositoryKeyHelper.ToOrderedRowKey(Guid.NewGuid()),
                Provider = provider,
                EventType = eventType,
                ProviderInvoiceId = providerInvoiceId,
                ProviderPaymentIntentId = providerPaymentIntentId,
                Amount = amount,
                Currency = string.IsNullOrWhiteSpace(currency) ? "USD" : currency.Trim().ToUpperInvariant(),
                Description = description,
                EffectiveUtc = effectiveUtc,
                IsDeleted = false
            }, ct);
        }

        private bool CanAttemptAutoTopUp(
            TenantBillingAccount? account,
            int topUpsThisCycle,
            int purchasedCreditsAvailable,
            DateTime effectiveUtc,
            out string? reason)
        {
            reason = null;

            if (account is null)
            {
                reason = "Automatic top-up skipped because the billing account is missing.";
                return false;
            }

            if (!account.AutoTopUpEnabled)
                return false;

            if (purchasedCreditsAvailable > Math.Max(0, account.TopUpTriggerThreshold))
                return false;

            if (string.IsNullOrWhiteSpace(account.TopUpPackSku))
            {
                reason = "Automatic top-up skipped because no top-up pack is configured.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(account.ProviderCustomerId) || string.IsNullOrWhiteSpace(account.DefaultPaymentMethodRef))
            {
                reason = "Automatic top-up skipped because billing customer or default payment method is missing.";
                return false;
            }

            if (account.MaxTopUpsPerCycle > 0 && topUpsThisCycle >= account.MaxTopUpsPerCycle)
            {
                reason = "Automatic top-up skipped because the cycle top-up limit has been reached.";
                return false;
            }

            if (account.LastAutoTopUpAttemptUtc.HasValue && effectiveUtc - account.LastAutoTopUpAttemptUtc.Value < AutoTopUpAttemptCooldown)
            {
                reason = "Automatic top-up skipped because a recent attempt is still cooling down.";
                return false;
            }

            if (account.LastAutoTopUpFailureUtc.HasValue && effectiveUtc - account.LastAutoTopUpFailureUtc.Value < AutoTopUpFailureCooldown)
            {
                reason = "Automatic top-up skipped because a recent failure is cooling down.";
                return false;
            }

            if (account.MaxTopUpSpendPerCycle.HasValue
                && _paymentProviderResolver.GetRequiredProvider(account.Provider).TryGetTopUpPriceAmount(account.TopUpPackSku, out var configuredPrice)
                && configuredPrice * (topUpsThisCycle + 1) > account.MaxTopUpSpendPerCycle.Value)
            {
                reason = "Automatic top-up skipped because the cycle spend cap would be exceeded.";
                return false;
            }

            return true;
        }

        private static string NormalizePeriodKey(string usagePeriodKey)
        {
            if (string.IsNullOrWhiteSpace(usagePeriodKey))
                throw new ArgumentException("UsagePeriodKey is required.", nameof(usagePeriodKey));

            return usagePeriodKey.Trim();
        }

        private static string TenantPartition(Guid tenantId) => EntityKeyPolicy.TenantPartition(tenantId);
        private static string PeriodPartition(Guid tenantId, string periodKey) => $"TENANT={tenantId:N}|PERIOD={periodKey}";
        private static string UserPeriodRow(Guid userId) => $"USER={userId:N}";

        private static DateTime MaxUtc(DateTime left, DateTime right) => left >= right ? left : right;

        private static string? BuildSourceReference(string? sourceReference, string suffix)
            => string.IsNullOrWhiteSpace(sourceReference) ? null : $"{sourceReference.Trim()}|{suffix}";

        private static string ToJsonStringLiteral(string? value)
            => value is null ? "null" : System.Text.Json.JsonSerializer.Serialize(value);
    }
}
