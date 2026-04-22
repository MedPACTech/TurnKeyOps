using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;
using Stripe;
using System.Text.Json;

namespace MedInsights.Services
{
    public sealed class StripePaymentProvider : IPaymentProvider
    {
        private readonly StripeClient _stripe;
        private readonly SystemSettings _systemSettings;
        private readonly StripeSettings _stripeSettings;
        private readonly StripeBillingCatalogSettings _catalogSettings;

        public StripePaymentProvider(
            IOptions<SystemSettings> systemSettings,
            IOptions<StripeSettings> stripeSettings,
            IOptions<StripeBillingCatalogSettings> catalogSettings,
            StripeClient stripe)
        {
            _stripe = stripe;
            _systemSettings = systemSettings.Value;
            _stripeSettings = stripeSettings.Value;
            _catalogSettings = catalogSettings.Value;
        }

        public string ProviderName => "Stripe";
        public bool CanHandleWebhooks => true;

        public async Task<PaymentCheckoutSessionDto> CreateSubscriptionCheckoutAsync(CreateSubscriptionCheckoutRequestDto dto, CancellationToken ct = default)
        {
            if (!_catalogSettings.SubscriptionPriceMap.TryGetValue(dto.PriceKey, out var priceId))
                throw new ArgumentException($"Unknown Stripe subscription price key '{dto.PriceKey}'.", nameof(dto.PriceKey));

            var service = new Stripe.Checkout.SessionService(_stripe);
            var session = await service.CreateAsync(new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "subscription",
                // Canonical post-checkout return path for self-serve onboarding.
                SuccessUrl = $"{_systemSettings.ApplicationHost}/registrationComplete?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{_systemSettings.MarketingDomain}/pricing",
                LineItems =
                [
                    new Stripe.Checkout.SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = dto.Quantity < 1 ? 1 : dto.Quantity
                    }
                ],
                AllowPromotionCodes = true,
                BillingAddressCollection = "auto",
                Metadata = new Dictionary<string, string>
                {
                    ["price_key"] = dto.PriceKey,
                    ["provider"] = ProviderName,
                    ["tenant_id"] = dto.TenantId?.ToString("D") ?? string.Empty,
                    ["requested_by_user_id"] = dto.RequestedByUserId?.ToString("D") ?? string.Empty
                }
            }, cancellationToken: ct);

            return new PaymentCheckoutSessionDto
            {
                Provider = ProviderName,
                SessionId = session.Id,
                Url = session.Url ?? string.Empty
            };
        }

        public async Task<PaymentPortalSessionDto> CreateCustomerPortalAsync(CreateCustomerPortalRequestDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.CustomerId))
                throw new ArgumentException("CustomerId is required.", nameof(dto.CustomerId));

            var service = new Stripe.BillingPortal.SessionService(_stripe);
            var session = await service.CreateAsync(new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = dto.CustomerId,
                ReturnUrl = $"{_systemSettings.ApplicationHost}/chats/"
            }, cancellationToken: ct);

            return new PaymentPortalSessionDto
            {
                Provider = ProviderName,
                Url = session.Url
            };
        }

        public async Task<PaymentSubscriptionResultDto> UpdateSubscriptionSeatsAsync(UpdateSubscriptionSeatsRequestDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.SubscriptionId))
                throw new ArgumentException("SubscriptionId is required.", nameof(dto.SubscriptionId));

            if (dto.SeatCount < 1)
                throw new ArgumentOutOfRangeException(nameof(dto.SeatCount), "SeatCount must be at least 1.");

            var service = new SubscriptionService(_stripe);
            var subscription = await service.GetAsync(dto.SubscriptionId, cancellationToken: ct);
            var itemId = subscription.Items.Data.FirstOrDefault()?.Id
                ?? throw new InvalidOperationException("Stripe subscription does not contain a subscription item.");

            var updated = await service.UpdateAsync(dto.SubscriptionId, new SubscriptionUpdateOptions
            {
                Items =
                [
                    new SubscriptionItemOptions
                    {
                        Id = itemId,
                        Quantity = dto.SeatCount
                    }
                ],
                ProrationBehavior = dto.ProrationBehavior
            }, cancellationToken: ct);

            return ToSubscriptionResult(updated);
        }

        public async Task<PaymentCheckoutSessionDto> PurchaseCreditTopUpAsync(PurchaseCreditTopUpRequestDto dto, CancellationToken ct = default)
        {
            if (!_catalogSettings.TopUpPriceMap.TryGetValue(dto.PriceKey, out var priceId))
                throw new ArgumentException($"Unknown Stripe top-up price key '{dto.PriceKey}'.", nameof(dto.PriceKey));

            var service = new Stripe.Checkout.SessionService(_stripe);
            var session = await service.CreateAsync(new Stripe.Checkout.SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = $"{_systemSettings.ApplicationHost}/billing/topup/success?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{_systemSettings.ApplicationHost}/billing",
                LineItems =
                [
                    new Stripe.Checkout.SessionLineItemOptions
                    {
                        Price = priceId,
                        Quantity = dto.Quantity < 1 ? 1 : dto.Quantity
                    }
                ],
                Metadata = new Dictionary<string, string>
                {
                    ["price_key"] = dto.PriceKey,
                    ["provider"] = ProviderName,
                    ["purchase_type"] = "credit_topup",
                    ["quantity"] = Math.Max(1, dto.Quantity).ToString(),
                    ["tenant_id"] = dto.TenantId?.ToString("D") ?? string.Empty,
                    ["requested_by_user_id"] = dto.RequestedByUserId?.ToString("D") ?? string.Empty
                }
            }, cancellationToken: ct);

            return new PaymentCheckoutSessionDto
            {
                Provider = ProviderName,
                SessionId = session.Id,
                Url = session.Url ?? string.Empty
            };
        }

        public async Task<PaymentTopUpResultDto> PurchaseCreditTopUpAutomaticallyAsync(AutoTopUpChargeRequestDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.CustomerId))
                throw new ArgumentException("CustomerId is required.", nameof(dto.CustomerId));
            if (string.IsNullOrWhiteSpace(dto.PaymentMethodId))
                throw new ArgumentException("PaymentMethodId is required.", nameof(dto.PaymentMethodId));

            var quantity = dto.Quantity < 1 ? 1 : dto.Quantity;
            var unitAmount = ResolveTopUpPriceAmount(dto.PriceKey);
            var service = new PaymentIntentService(_stripe);

            try
            {
                var paymentIntent = await service.CreateAsync(new PaymentIntentCreateOptions
                {
                    Customer = dto.CustomerId,
                    PaymentMethod = dto.PaymentMethodId,
                    Amount = (long)Math.Round(unitAmount * quantity * 100m, MidpointRounding.AwayFromZero),
                    Currency = "usd",
                    Confirm = true,
                    OffSession = true,
                    Metadata = new Dictionary<string, string>
                    {
                        ["price_key"] = dto.PriceKey,
                        ["provider"] = ProviderName,
                        ["purchase_type"] = "credit_topup_auto",
                        ["quantity"] = quantity.ToString(),
                        ["tenant_id"] = dto.TenantId.ToString("D"),
                        ["requested_by_user_id"] = dto.RequestedByUserId?.ToString("D") ?? string.Empty
                    }
                }, cancellationToken: ct);

                return new PaymentTopUpResultDto
                {
                    Provider = ProviderName,
                    PriceKey = dto.PriceKey,
                    Success = string.Equals(paymentIntent.Status, "succeeded", StringComparison.OrdinalIgnoreCase),
                    Quantity = quantity,
                    Amount = unitAmount * quantity,
                    Currency = "USD",
                    PaymentIntentId = paymentIntent.Id
                };
            }
            catch (StripeException ex)
            {
                return new PaymentTopUpResultDto
                {
                    Provider = ProviderName,
                    PriceKey = dto.PriceKey,
                    Success = false,
                    Quantity = quantity,
                    Amount = unitAmount * quantity,
                    Currency = "USD",
                    ErrorCode = ex.StripeError?.Code,
                    ErrorMessage = ex.StripeError?.Message ?? ex.Message
                };
            }
        }

        public async Task<PaymentSubscriptionResultDto> CancelAtTermEndAsync(CancelSubscriptionRequestDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.SubscriptionId))
                throw new ArgumentException("SubscriptionId is required.", nameof(dto.SubscriptionId));

            var service = new SubscriptionService(_stripe);
            var updated = await service.UpdateAsync(dto.SubscriptionId, new SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = true
            }, cancellationToken: ct);

            return ToSubscriptionResult(updated);
        }

        public async Task<PaymentSubscriptionResultDto> ReactivateAsync(ReactivateSubscriptionRequestDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(dto.SubscriptionId))
                throw new ArgumentException("SubscriptionId is required.", nameof(dto.SubscriptionId));

            var service = new SubscriptionService(_stripe);
            var updated = await service.UpdateAsync(dto.SubscriptionId, new SubscriptionUpdateOptions
            {
                CancelAtPeriodEnd = false
            }, cancellationToken: ct);

            return ToSubscriptionResult(updated);
        }

        public Task<PaymentWebhookEventDto> ParseWebhookAsync(string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_stripeSettings.WebhookSecret))
                throw new InvalidOperationException("Stripe webhook secret is not configured.");

            headers.TryGetValue("Stripe-Signature", out var signatureHeader);
            // Stripe CLI and dashboard webhooks can arrive with an API version newer than the SDK's
            // pinned default. Validate the signature, but do not fail local processing on version mismatch.
            var stripeEvent = EventUtility.ConstructEvent(
                json,
                signatureHeader,
                _stripeSettings.WebhookSecret,
                throwOnApiVersionMismatch: false);
            var customerId = TryGetCustomerId(stripeEvent);
            var subscriptionId = TryGetSubscriptionId(stripeEvent) ?? ExtractSubscriptionId(json);

            return Task.FromResult(new PaymentWebhookEventDto
            {
                Provider = ProviderName,
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                PayloadJson = json,
                CustomerId = customerId,
                SubscriptionId = subscriptionId,
                TenantId = ExtractGuidMetadata(json, "tenant_id"),
                RequestedByUserId = ExtractGuidMetadata(json, "requested_by_user_id"),
                PriceKey = ExtractStringMetadata(json, "price_key"),
                PurchaseType = ExtractStringMetadata(json, "purchase_type"),
                Mode = ExtractString(json, "data", "object", "mode"),
                Status = ExtractStatus(json, stripeEvent.Type),
                Quantity = ExtractIntMetadata(json, "quantity") ?? ExtractQuantity(json),
                SeatCount = ExtractSeatCount(json),
                CurrentPeriodStartUtc = ExtractCurrentPeriodStartUtc(json),
                CurrentPeriodEndUtc = ExtractCurrentPeriodEndUtc(json),
                CancelAtPeriodEnd = ExtractBoolean(json, "data", "object", "cancel_at_period_end")
            });
        }

        public bool TryGetTopUpPriceAmount(string priceKey, out decimal amount)
            => _catalogSettings.TopUpPriceAmountMap.TryGetValue(priceKey, out amount);

        public bool TryGetTopUpCreditAmount(string priceKey, out int amount)
            => _catalogSettings.TopUpCreditAmountMap.TryGetValue(priceKey, out amount);

        private static PaymentSubscriptionResultDto ToSubscriptionResult(Subscription subscription)
        {
            var seatCount = subscription.Items.Data.FirstOrDefault()?.Quantity ?? 0;

            return new PaymentSubscriptionResultDto
            {
                Provider = "Stripe",
                SubscriptionId = subscription.Id,
                Status = subscription.Status,
                SeatCount = (int)seatCount,
                CancelAtPeriodEnd = subscription.CancelAtPeriodEnd,
                CurrentPeriodStartUtc = null,
                CurrentPeriodEndUtc = null
            };
        }

        private static string? TryGetCustomerId(Event stripeEvent) =>
            stripeEvent.Data.Object switch
            {
                Stripe.Checkout.Session session => session.CustomerId,
                Subscription subscription => subscription.CustomerId,
                Invoice invoice => invoice.CustomerId,
                _ => null
            };

        private static string? TryGetSubscriptionId(Event stripeEvent) =>
            stripeEvent.Data.Object switch
            {
                Stripe.Checkout.Session session => session.SubscriptionId,
                Subscription subscription => subscription.Id,
                Invoice => null,
                _ => null
            };

        private static Guid? ExtractGuidMetadata(string json, string key)
        {
            var value = ExtractStringMetadata(json, key);
            return Guid.TryParse(value, out var parsed) ? parsed : null;
        }

        private static string? ExtractStringMetadata(string json, string key)
        {
            using var document = JsonDocument.Parse(json);
            if (TryGetNestedProperty(document.RootElement, out var metadata, "data", "object", "metadata")
                && metadata.ValueKind == JsonValueKind.Object
                && metadata.TryGetProperty(key, out var value)
                && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            return null;
        }

        private static int? ExtractIntMetadata(string json, string key)
        {
            var value = ExtractStringMetadata(json, key);
            return int.TryParse(value, out var parsed) ? parsed : null;
        }

        private static int? ExtractQuantity(string json)
            => ExtractSeatCount(json);

        private static string? ExtractString(string json, params string[] path)
        {
            using var document = JsonDocument.Parse(json);
            return TryGetNestedProperty(document.RootElement, out var value, path) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }

        private static string? ExtractStatus(string json, string eventType)
        {
            using var document = JsonDocument.Parse(json);
            if (eventType.StartsWith("invoice.", StringComparison.OrdinalIgnoreCase))
            {
                return TryGetNestedProperty(document.RootElement, out var invoiceStatus, "data", "object", "status")
                    && invoiceStatus.ValueKind == JsonValueKind.String
                    ? invoiceStatus.GetString()
                    : null;
            }

            return TryGetNestedProperty(document.RootElement, out var status, "data", "object", "status")
                && status.ValueKind == JsonValueKind.String
                ? status.GetString()
                : null;
        }

        private static int? ExtractSeatCount(string json)
        {
            using var document = JsonDocument.Parse(json);
            if (TryGetNestedProperty(document.RootElement, out var quantity, "data", "object", "items", "data", "0", "quantity")
                && quantity.TryGetInt32(out var parsed))
            {
                return parsed;
            }

            if (TryGetNestedProperty(document.RootElement, out quantity, "data", "object", "quantity")
                && quantity.TryGetInt32(out parsed))
            {
                return parsed;
            }

            if (TryGetNestedProperty(document.RootElement, out quantity, "data", "object", "lines", "data", "0", "quantity")
                && quantity.TryGetInt32(out parsed))
            {
                return parsed;
            }

            return null;
        }

        private static DateTime? ExtractUnixTimestamp(string json, params string[] path)
        {
            using var document = JsonDocument.Parse(json);
            return TryGetNestedProperty(document.RootElement, out var value, path) && value.TryGetInt64(out var unix)
                ? DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime
                : null;
        }

        private static DateTime? ExtractCurrentPeriodStartUtc(string json)
            => ExtractUnixTimestamp(json, "data", "object", "current_period_start")
               ?? ExtractUnixTimestamp(json, "data", "object", "items", "data", "0", "current_period_start")
               ?? ExtractUnixTimestamp(json, "data", "object", "lines", "data", "0", "period", "start");

        private static DateTime? ExtractCurrentPeriodEndUtc(string json)
            => ExtractUnixTimestamp(json, "data", "object", "current_period_end")
               ?? ExtractUnixTimestamp(json, "data", "object", "items", "data", "0", "current_period_end")
               ?? ExtractUnixTimestamp(json, "data", "object", "lines", "data", "0", "period", "end");

        private static string? ExtractSubscriptionId(string json)
            => ExtractString(json, "data", "object", "subscription")
               ?? ExtractString(json, "data", "object", "parent", "subscription_details", "subscription");

        private static bool? ExtractBoolean(string json, params string[] path)
        {
            using var document = JsonDocument.Parse(json);
            return TryGetNestedProperty(document.RootElement, out var value, path)
                && (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                ? value.GetBoolean()
                : null;
        }

        private static bool TryGetNestedProperty(JsonElement element, out JsonElement value, params string[] path)
        {
            value = element;
            foreach (var segment in path)
            {
                if (value.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index))
                {
                    if (index < 0 || index >= value.GetArrayLength())
                        return false;

                    value = value[index];
                    continue;
                }

                if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(segment, out value))
                    return false;
            }

            return true;
        }

        private decimal ResolveTopUpPriceAmount(string priceKey)
        {
            if (_catalogSettings.TopUpPriceAmountMap.TryGetValue(priceKey, out var amount))
                return amount;

            throw new ArgumentException($"Unknown Stripe top-up amount key '{priceKey}'.", nameof(priceKey));
        }
    }
}
