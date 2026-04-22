using Azure.Messaging.ServiceBus;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class CreditUsageDispatchService : ICreditUsageDispatchService
    {
        private const string QueueName = "credit-usage";
        private readonly ServiceBusClient _serviceBusClient;
        private readonly IProcessingCreditUsageRepository _processingCreditUsageRepository;

        public CreditUsageDispatchService(
            ServiceBusClient serviceBusClient,
            IProcessingCreditUsageRepository processingCreditUsageRepository)
        {
            _serviceBusClient = serviceBusClient;
            _processingCreditUsageRepository = processingCreditUsageRepository;
        }

        public async Task<Guid> EnqueueAsync(CreditUsageMessageDto dto, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var requestId = dto.RequestId == Guid.Empty ? Guid.NewGuid() : dto.RequestId;
            dto.RequestId = requestId;

            var existing = await _processingCreditUsageRepository.GetByRequestIdAsync(requestId, ct);
            if (existing is null)
            {
                await _processingCreditUsageRepository.SaveAsync(new ProcessingCreditUsage
                {
                    Id = requestId,
                    RequestId = requestId,
                    TenantId = dto.TenantId,
                    UserId = dto.UserId,
                    UsagePeriodKey = dto.UsagePeriodKey,
                    Credits = dto.Credits,
                    SourceReference = dto.SourceReference,
                    Description = dto.Description,
                    RequestedAtUtc = DateTime.UtcNow,
                    EffectiveUtc = dto.EffectiveUtc,
                    Completed = false,
                    PartitionKey = EntityKeyPolicy.TenantPartition(dto.TenantId),
                    RowKey = RepositoryKeyHelper.ToOrderedRowKey(requestId),
                    IsDeleted = false
                }, ct);
            }

            var sender = _serviceBusClient.CreateSender(QueueName);
            var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(dto))
            {
                MessageId = requestId.ToString("D"),
                CorrelationId = dto.TenantId.ToString("D"),
                Subject = "credit_usage"
            };

            await sender.SendMessageAsync(message, ct);
            return requestId;
        }
    }
}
