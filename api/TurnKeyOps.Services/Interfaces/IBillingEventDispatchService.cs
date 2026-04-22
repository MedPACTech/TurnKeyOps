using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IBillingEventDispatchService
    {
        Task EnqueueAsync(PaymentWebhookEventDto dto, CancellationToken ct = default);
    }
}
