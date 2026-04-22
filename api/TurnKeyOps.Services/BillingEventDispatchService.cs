using Azure.Messaging.ServiceBus;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class BillingEventDispatchService : IBillingEventDispatchService
    {
        private const string QueueName = "billing-events";
        private readonly ServiceBusClient _serviceBusClient;

        public BillingEventDispatchService(ServiceBusClient serviceBusClient)
        {
            _serviceBusClient = serviceBusClient;
        }

        public async Task EnqueueAsync(PaymentWebhookEventDto dto, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var sender = _serviceBusClient.CreateSender(QueueName);
            var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(dto))
            {
                MessageId = $"{dto.Provider}:{dto.EventId}",
                CorrelationId = dto.TenantId?.ToString("D"),
                Subject = dto.EventType
            };

            await sender.SendMessageAsync(message, ct);
        }
    }
}
