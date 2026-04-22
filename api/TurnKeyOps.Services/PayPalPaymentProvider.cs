using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;

namespace MedInsights.Services
{
    public sealed class PayPalPaymentProvider : IPaymentProvider
    {
        private readonly HttpClient _httpClient;
        private readonly PayPalSettings _payPalSettings;
        private readonly PayPalBillingCatalogSettings _catalogSettings;
        private readonly SystemSettings _systemSettings;

        public PayPalPaymentProvider(
            HttpClient httpClient,
            IOptions<PayPalSettings> payPalSettings,
            IOptions<PayPalBillingCatalogSettings> catalogSettings,
            IOptions<SystemSettings> systemSettings)
        {
            _httpClient = httpClient;
            _payPalSettings = payPalSettings.Value;
            _catalogSettings = catalogSettings.Value;
            _systemSettings = systemSettings.Value;
        }

        public string ProviderName => "PayPal";
        public bool CanHandleWebhooks => true;

        public async Task<PaymentCheckoutSessionDto> CreateSubscriptionCheckoutAsync(CreateSubscriptionCheckoutRequestDto dto, CancellationToken ct = default)
        {
            if (!_catalogSettings.SubscriptionPlanMap.TryGetValue(dto.PriceKey, out var planId))
                throw new ArgumentException($"Unknown PayPal subscription price key '{dto.PriceKey}'.", nameof(dto.PriceKey));

            var response = await SendAsync(
                HttpMethod.Post,
                "/v1/billing/subscriptions",
                new
                {
                    plan_id = planId,
                    quantity = Math.Max(1, dto.Quantity).ToString(),
                    custom_id = BuildCustomId(dto.TenantId, dto.RequestedByUserId, dto.PriceKey, "subscription", dto.Quantity),
                    application_context = new
                    {
                        return_url = $"{_systemSettings.ApplicationHost}/signup/success",
                        cancel_url = $"{_systemSettings.MarketingDomain}/pricing"
                    }
                },
                ct);

            using var document = JsonDocument.Parse(response);
            return new PaymentCheckoutSessionDto
            {
                Provider = ProviderName,
                SessionId = TryReadString(document.RootElement, "id") ?? string.Empty,
                Url = GetLink(document.RootElement, "approve")
                    ?? throw new InvalidOperationException("PayPal subscription response did not include an approval URL.")
            };
        }

        public Task<PaymentPortalSessionDto> CreateCustomerPortalAsync(CreateCustomerPortalRequestDto dto, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_payPalSettings.CustomerPortalUrl))
                throw new InvalidOperationException("PayPal customer portal URL is not configured.");

            return Task.FromResult(new PaymentPortalSessionDto
            {
                Provider = ProviderName,
                Url = _payPalSettings.CustomerPortalUrl
            });
        }

        public async Task<PaymentSubscriptionResultDto> UpdateSubscriptionSeatsAsync(UpdateSubscriptionSeatsRequestDto dto, CancellationToken ct = default)
        {
            var detailJson = await SendAsync(HttpMethod.Get, $"/v1/billing/subscriptions/{dto.SubscriptionId}", null, ct);
            using var detail = JsonDocument.Parse(detailJson);
            var planId = TryReadString(detail.RootElement, "plan_id");
            if (string.IsNullOrWhiteSpace(planId))
                throw new InvalidOperationException("PayPal subscription is missing a plan identifier.");

            await SendAsync(
                HttpMethod.Post,
                $"/v1/billing/subscriptions/{dto.SubscriptionId}/revise",
                new
                {
                    plan_id = planId,
                    quantity = Math.Max(1, dto.SeatCount).ToString()
                },
                ct);

            return new PaymentSubscriptionResultDto
            {
                Provider = ProviderName,
                SubscriptionId = dto.SubscriptionId,
                Status = TryReadString(detail.RootElement, "status") ?? "APPROVAL_PENDING",
                SeatCount = dto.SeatCount,
                CancelAtPeriodEnd = false,
                CurrentPeriodStartUtc = TryReadTime(detail.RootElement, "start_time"),
                CurrentPeriodEndUtc = TryReadTime(detail.RootElement, "billing_info", "next_billing_time")
            };
        }

        public async Task<PaymentCheckoutSessionDto> PurchaseCreditTopUpAsync(PurchaseCreditTopUpRequestDto dto, CancellationToken ct = default)
        {
            if (!_catalogSettings.TopUpPriceAmountMap.TryGetValue(dto.PriceKey, out var unitAmount))
                throw new ArgumentException($"Unknown PayPal top-up price key '{dto.PriceKey}'.", nameof(dto.PriceKey));

            var quantity = Math.Max(1, dto.Quantity);
            var total = unitAmount * quantity;
            var response = await SendAsync(
                HttpMethod.Post,
                "/v2/checkout/orders",
                new
                {
                    intent = "CAPTURE",
                    purchase_units = new[]
                    {
                        new
                        {
                            reference_id = dto.PriceKey,
                            custom_id = BuildCustomId(dto.TenantId, dto.RequestedByUserId, dto.PriceKey, "credit_topup", quantity),
                            amount = new
                            {
                                currency_code = "USD",
                                value = total.ToString("0.00")
                            },
                            description = $"Credit top-up {dto.PriceKey}"
                        }
                    },
                    application_context = new
                    {
                        return_url = $"{_systemSettings.ApplicationHost}/billing/topup/success",
                        cancel_url = $"{_systemSettings.ApplicationHost}/billing"
                    }
                },
                ct);

            using var document = JsonDocument.Parse(response);
            return new PaymentCheckoutSessionDto
            {
                Provider = ProviderName,
                SessionId = TryReadString(document.RootElement, "id") ?? string.Empty,
                Url = GetLink(document.RootElement, "approve")
                    ?? throw new InvalidOperationException("PayPal order response did not include an approval URL.")
            };
        }

        public async Task<PaymentTopUpResultDto> PurchaseCreditTopUpAutomaticallyAsync(AutoTopUpChargeRequestDto dto, CancellationToken ct = default)
        {
            if (!_catalogSettings.TopUpPriceAmountMap.TryGetValue(dto.PriceKey, out var unitAmount))
                throw new ArgumentException($"Unknown PayPal top-up price key '{dto.PriceKey}'.", nameof(dto.PriceKey));

            var quantity = Math.Max(1, dto.Quantity);
            var total = unitAmount * quantity;

            try
            {
                var response = await SendAsync(
                    HttpMethod.Post,
                    "/v2/checkout/orders",
                    new
                    {
                        intent = "CAPTURE",
                        payment_source = new
                        {
                            paypal = new
                            {
                                vault_id = dto.PaymentMethodId
                            }
                        },
                        purchase_units = new[]
                        {
                            new
                            {
                                reference_id = dto.PriceKey,
                                custom_id = BuildCustomId(dto.TenantId, dto.RequestedByUserId, dto.PriceKey, "credit_topup_auto", quantity),
                                amount = new
                                {
                                    currency_code = "USD",
                                    value = total.ToString("0.00")
                                },
                                description = $"Automatic credit top-up {dto.PriceKey}"
                            }
                        }
                    },
                    ct);

                using var document = JsonDocument.Parse(response);
                return new PaymentTopUpResultDto
                {
                    Provider = ProviderName,
                    PriceKey = dto.PriceKey,
                    Success = string.Equals(TryReadString(document.RootElement, "status"), "COMPLETED", StringComparison.OrdinalIgnoreCase),
                    Quantity = quantity,
                    Amount = total,
                    Currency = "USD",
                    PaymentIntentId = TryReadString(document.RootElement, "id"),
                    InvoiceId = TryReadCaptureId(document.RootElement)
                };
            }
            catch (HttpRequestException ex)
            {
                return new PaymentTopUpResultDto
                {
                    Provider = ProviderName,
                    PriceKey = dto.PriceKey,
                    Success = false,
                    Quantity = quantity,
                    Amount = total,
                    Currency = "USD",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<PaymentSubscriptionResultDto> CancelAtTermEndAsync(CancelSubscriptionRequestDto dto, CancellationToken ct = default)
        {
            await SendAsync(HttpMethod.Post, $"/v1/billing/subscriptions/{dto.SubscriptionId}/cancel", new
            {
                reason = "Canceled from MedInsights billing."
            }, ct);

            return new PaymentSubscriptionResultDto
            {
                Provider = ProviderName,
                SubscriptionId = dto.SubscriptionId,
                Status = "CANCELLED",
                CancelAtPeriodEnd = true
            };
        }

        public async Task<PaymentSubscriptionResultDto> ReactivateAsync(ReactivateSubscriptionRequestDto dto, CancellationToken ct = default)
        {
            await SendAsync(HttpMethod.Post, $"/v1/billing/subscriptions/{dto.SubscriptionId}/activate", new
            {
                reason = "Reactivated from MedInsights billing."
            }, ct);

            var detailJson = await SendAsync(HttpMethod.Get, $"/v1/billing/subscriptions/{dto.SubscriptionId}", null, ct);
            using var detail = JsonDocument.Parse(detailJson);
            return new PaymentSubscriptionResultDto
            {
                Provider = ProviderName,
                SubscriptionId = dto.SubscriptionId,
                Status = TryReadString(detail.RootElement, "status") ?? "ACTIVE",
                SeatCount = TryReadQuantity(detail.RootElement),
                CancelAtPeriodEnd = false,
                CurrentPeriodStartUtc = TryReadTime(detail.RootElement, "start_time"),
                CurrentPeriodEndUtc = TryReadTime(detail.RootElement, "billing_info", "next_billing_time")
            };
        }

        public async Task<PaymentWebhookEventDto> ParseWebhookAsync(string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
        {
            await VerifyWebhookAsync(json, headers, ct);

            using var document = JsonDocument.Parse(json);
            var resource = document.RootElement.TryGetProperty("resource", out var resourceElement)
                ? resourceElement
                : default;
            var metadata = ParseCustomId(TryReadString(resource, "custom_id"));
            var tenantId = metadata.TryGetValue("tenant_id", out var tenantValue) && Guid.TryParse(tenantValue, out var parsedTenantId)
                ? parsedTenantId
                : (Guid?)null;
            var requestedByUserId = metadata.TryGetValue("requested_by_user_id", out var userValue) && Guid.TryParse(userValue, out var parsedUserId)
                ? parsedUserId
                : (Guid?)null;
            var quantity = metadata.TryGetValue("quantity", out var quantityValue) && int.TryParse(quantityValue, out var parsedQuantity)
                ? parsedQuantity
                : (int?)null;

            return new PaymentWebhookEventDto
            {
                Provider = ProviderName,
                EventId = TryReadString(document.RootElement, "id") ?? string.Empty,
                EventType = TryReadString(document.RootElement, "event_type") ?? string.Empty,
                PayloadJson = json,
                CustomerId = TryReadString(resource, "subscriber", "payer_id") ?? TryReadString(resource, "payer", "payer_id"),
                SubscriptionId = TryReadString(resource, "id"),
                TenantId = tenantId,
                RequestedByUserId = requestedByUserId,
                PriceKey = metadata.TryGetValue("price_key", out var priceKey) ? priceKey : null,
                PurchaseType = metadata.TryGetValue("purchase_type", out var purchaseType) ? purchaseType : null,
                Mode = InferMode(TryReadString(document.RootElement, "event_type")),
                Status = TryReadString(resource, "status"),
                Quantity = quantity ?? TryReadNullableQuantity(resource),
                SeatCount = TryReadNullableQuantity(resource),
                CurrentPeriodStartUtc = TryReadTime(resource, "start_time"),
                CurrentPeriodEndUtc = TryReadTime(resource, "billing_info", "next_billing_time"),
                CancelAtPeriodEnd = string.Equals(TryReadString(document.RootElement, "event_type"), "BILLING.SUBSCRIPTION.CANCELLED", StringComparison.OrdinalIgnoreCase)
            };
        }

        public bool TryGetTopUpPriceAmount(string priceKey, out decimal amount)
            => _catalogSettings.TopUpPriceAmountMap.TryGetValue(priceKey, out amount);

        public bool TryGetTopUpCreditAmount(string priceKey, out int amount)
            => _catalogSettings.TopUpCreditAmountMap.TryGetValue(priceKey, out amount);

        private async Task VerifyWebhookAsync(string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_payPalSettings.WebhookId))
                throw new InvalidOperationException("PayPal webhook ID is not configured.");

            var response = await SendAsync(
                HttpMethod.Post,
                "/v1/notifications/verify-webhook-signature",
                new
                {
                    auth_algo = GetRequiredHeader(headers, "PAYPAL-AUTH-ALGO"),
                    cert_url = GetRequiredHeader(headers, "PAYPAL-CERT-URL"),
                    transmission_id = GetRequiredHeader(headers, "PAYPAL-TRANSMISSION-ID"),
                    transmission_sig = GetRequiredHeader(headers, "PAYPAL-TRANSMISSION-SIG"),
                    transmission_time = GetRequiredHeader(headers, "PAYPAL-TRANSMISSION-TIME"),
                    webhook_id = _payPalSettings.WebhookId,
                    webhook_event = JsonSerializer.Deserialize<JsonElement>(json)
                },
                ct);

            using var document = JsonDocument.Parse(response);
            if (!string.Equals(TryReadString(document.RootElement, "verification_status"), "SUCCESS", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("PayPal webhook verification failed.");
        }

        private async Task<string> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync(ct));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            if (body is not null)
                request.Content = JsonContent.Create(body);

            using var response = await _httpClient.SendAsync(request, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"PayPal request to '{path}' failed with status {(int)response.StatusCode}: {payload}");

            return payload;
        }

        private async Task<string> GetAccessTokenAsync(CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(_payPalSettings.ClientId) || string.IsNullOrWhiteSpace(_payPalSettings.ClientSecret))
                throw new InvalidOperationException("PayPal client credentials are not configured.");

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/oauth2/token");
            request.Headers.Authorization = new AuthenticationHeaderValue(
                "Basic",
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_payPalSettings.ClientId}:{_payPalSettings.ClientSecret}")));
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials"
            });

            using var response = await _httpClient.SendAsync(request, ct);
            var payload = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException($"PayPal OAuth token request failed with status {(int)response.StatusCode}: {payload}");

            using var document = JsonDocument.Parse(payload);
            return TryReadString(document.RootElement, "access_token")
                ?? throw new InvalidOperationException("PayPal OAuth token response did not include an access token.");
        }

        private static string BuildCustomId(Guid? tenantId, Guid? requestedByUserId, string priceKey, string purchaseType, int quantity)
            => string.Join(";", new[]
            {
                $"tenant_id={tenantId?.ToString("D") ?? string.Empty}",
                $"requested_by_user_id={requestedByUserId?.ToString("D") ?? string.Empty}",
                $"price_key={priceKey}",
                $"purchase_type={purchaseType}",
                $"quantity={Math.Max(1, quantity)}"
            });

        private static Dictionary<string, string> ParseCustomId(string? customId)
        {
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(customId))
                return values;

            foreach (var segment in customId.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var splitIndex = segment.IndexOf('=');
                if (splitIndex <= 0 || splitIndex >= segment.Length - 1)
                    continue;

                values[segment[..splitIndex]] = segment[(splitIndex + 1)..];
            }

            return values;
        }

        private static string? GetLink(JsonElement element, string rel)
        {
            if (!element.TryGetProperty("links", out var links) || links.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var link in links.EnumerateArray())
            {
                if (string.Equals(TryReadString(link, "rel"), rel, StringComparison.OrdinalIgnoreCase))
                    return TryReadString(link, "href");
            }

            return null;
        }

        private static string GetRequiredHeader(IReadOnlyDictionary<string, string> headers, string key)
        {
            if (headers.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return value;

            throw new InvalidOperationException($"Missing PayPal webhook header '{key}'.");
        }

        private static string InferMode(string? eventType)
            => !string.IsNullOrWhiteSpace(eventType) && eventType.StartsWith("PAYMENT.", StringComparison.OrdinalIgnoreCase)
                ? "payment"
                : "subscription";

        private static int TryReadQuantity(JsonElement element)
            => TryReadNullableQuantity(element) ?? 0;

        private static int? TryReadNullableQuantity(JsonElement element)
        {
            var raw = TryReadString(element, "quantity");
            return int.TryParse(raw, out var parsed) ? parsed : null;
        }

        private static DateTime? TryReadTime(JsonElement element, params string[] path)
        {
            var raw = TryReadString(element, path);
            return DateTime.TryParse(raw, out var parsed) ? parsed.ToUniversalTime() : null;
        }

        private static string? TryReadString(JsonElement element, params string[] path)
        {
            var current = element;
            foreach (var segment in path)
            {
                if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, out var index))
                {
                    if (index < 0 || index >= current.GetArrayLength())
                        return null;

                    current = current[index];
                    continue;
                }

                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                    return null;
            }

            return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
        }

        private static string? TryReadCaptureId(JsonElement element)
            => TryReadString(element, "purchase_units", "0", "payments", "captures", "0", "id");
    }
}
