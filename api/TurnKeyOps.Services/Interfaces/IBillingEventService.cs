using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IBillingEventService
    {
        Task HandleWebhookAsync(PaymentWebhookEventDto dto, CancellationToken ct = default);
    }
}
