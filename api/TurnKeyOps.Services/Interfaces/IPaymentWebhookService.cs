using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPaymentWebhookService
    {
        Task<WebhookEventDto> ReceiveStripeWebhookAsync(string json, string? signatureHeader, CancellationToken ct = default);
        Task<WebhookEventDto> ReceiveWebhookAsync(string provider, string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);
    }
}
