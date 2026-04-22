using System.Security.Cryptography;
using System.Text;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using MedInsights.Services.Mappers;

namespace MedInsights.Services
{
    public sealed class PaymentWebhookService : IPaymentWebhookService
    {
        private readonly IPaymentProviderResolver _paymentProviderResolver;
        private readonly IWebhookEventRepository _webhookEventRepository;
        private readonly IBillingEventDispatchService _billingEventDispatchService;
        private readonly IOperationalAlertService _alertService;
        private readonly IAuditService _auditService;

        public PaymentWebhookService(
            IPaymentProviderResolver paymentProviderResolver,
            IWebhookEventRepository webhookEventRepository,
            IBillingEventDispatchService billingEventDispatchService,
            IOperationalAlertService alertService,
            IAuditService auditService)
        {
            _paymentProviderResolver = paymentProviderResolver;
            _webhookEventRepository = webhookEventRepository;
            _billingEventDispatchService = billingEventDispatchService;
            _alertService = alertService;
            _auditService = auditService;
        }

        public async Task<WebhookEventDto> ReceiveStripeWebhookAsync(string json, string? signatureHeader, CancellationToken ct = default)
            => await ReceiveWebhookAsync("Stripe", json, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Stripe-Signature"] = signatureHeader ?? string.Empty
            }, ct);

        public async Task<WebhookEventDto> ReceiveWebhookAsync(string provider, string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default)
        {
            var paymentProvider = _paymentProviderResolver.GetRequiredProvider(provider);
            var parsedEvent = await paymentProvider.ParseWebhookAsync(json, headers, ct);
            var partitionKey = parsedEvent.Provider.ToUpperInvariant();
            var rowKey = parsedEvent.EventId;

            var existing = await _webhookEventRepository.GetAsync(partitionKey, rowKey, ct);
            if (existing is not null)
                return WebhookEventMapper.ToDto(existing);

            var now = DateTime.UtcNow;
            var entity = new WebhookEvent
            {
                Id = Guid.NewGuid(),
                PartitionKey = partitionKey,
                RowKey = rowKey,
                Provider = parsedEvent.Provider,
                EventType = parsedEvent.EventType,
                ProcessingStatus = "Received",
                CorrelationTenantId = parsedEvent.TenantId,
                PayloadHash = ComputeSha256(json),
                ReceivedUtc = now,
                ProcessedUtc = null,
                IsDeleted = false
            };

            var saved = await _webhookEventRepository.SaveAsync(entity, ct);
            try
            {
                await _billingEventDispatchService.EnqueueAsync(parsedEvent, ct);
                saved.ProcessingStatus = "Queued";
                await _auditService.RecordAsync(new RecordAuditEventRequestDto
                {
                    TenantId = parsedEvent.TenantId,
                    Category = "billing",
                    Action = "webhook_queued",
                    Severity = "info",
                    TargetType = "webhook_event",
                    TargetId = parsedEvent.EventId,
                    Source = nameof(PaymentWebhookService),
                    Description = $"Queued {parsedEvent.Provider} webhook {parsedEvent.EventType} for background processing."
                }, ct);
            }
            catch
            {
                saved.ProcessingStatus = "Failed";
                saved.ProcessedUtc = DateTime.UtcNow;
                await _webhookEventRepository.SaveAsync(saved, ct);
                await _alertService.RaiseAsync(new RaiseOperationalAlertRequestDto
                {
                    TenantId = parsedEvent.TenantId,
                    AlertType = "webhook_failure",
                    Severity = "error",
                    DedupeKey = $"{parsedEvent.Provider}:{parsedEvent.EventId}:failed",
                    Source = nameof(PaymentWebhookService),
                    Message = $"Failed queueing {parsedEvent.Provider} webhook {parsedEvent.EventType}.",
                    ContextJson = parsedEvent.PayloadJson
                }, ct);
                throw;
            }

            saved = await _webhookEventRepository.SaveAsync(saved, ct);
            return WebhookEventMapper.ToDto(saved);
        }

        private static string ComputeSha256(string payload)
        {
            var bytes = Encoding.UTF8.GetBytes(payload);
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash);
        }
    }
}
