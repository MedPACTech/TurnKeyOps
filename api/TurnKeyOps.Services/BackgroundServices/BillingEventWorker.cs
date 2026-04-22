using Azure.Messaging.ServiceBus;
using MedInsights.Lib.Dtos;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedInsights.Services.BackgroundServices
{
    public sealed class BillingEventWorker : BackgroundService
    {
        private const string QueueName = "billing-events";
        private readonly ServiceBusProcessor _processor;
        private readonly ILogger<BillingEventWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public BillingEventWorker(
            ServiceBusClient serviceBusClient,
            ILogger<BillingEventWorker> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _processor = serviceBusClient.CreateProcessor(QueueName, new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 2,
                AutoCompleteMessages = false
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _processor.ProcessMessageAsync += HandleMessageAsync;
            _processor.ProcessErrorAsync += HandleErrorAsync;

            _logger.LogInformation("BillingEventWorker started, listening on '{QueueName}' queue.", QueueName);
            await _processor.StartProcessingAsync(stoppingToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
            await base.StopAsync(cancellationToken);
        }

        private async Task HandleMessageAsync(ProcessMessageEventArgs args)
        {
            PaymentWebhookEventDto? dto = null;

            try
            {
                dto = args.Message.Body.ToObjectFromJson<PaymentWebhookEventDto>();
                if (dto is null || string.IsNullOrWhiteSpace(dto.Provider) || string.IsNullOrWhiteSpace(dto.EventId))
                {
                    await args.DeadLetterMessageAsync(args.Message, cancellationToken: args.CancellationToken);
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var webhookEventRepository = scope.ServiceProvider.GetRequiredService<IWebhookEventRepository>();
                var billingEventService = scope.ServiceProvider.GetRequiredService<IBillingEventService>();
                var alertService = scope.ServiceProvider.GetRequiredService<IOperationalAlertService>();
                var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

                var partitionKey = dto.Provider.ToUpperInvariant();
                var webhookEvent = await webhookEventRepository.GetAsync(partitionKey, dto.EventId, args.CancellationToken);
                if (webhookEvent is null)
                {
                    await args.DeadLetterMessageAsync(args.Message, "WebhookEventNotFound", "No persisted webhook receipt was found for this billing event.", args.CancellationToken);
                    return;
                }

                if (string.Equals(webhookEvent.ProcessingStatus, "Processed", StringComparison.OrdinalIgnoreCase))
                {
                    await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                    return;
                }

                webhookEvent.ProcessingStatus = "Processing";
                webhookEvent.CorrelationTenantId = dto.TenantId;
                webhookEvent.ProcessedUtc = null;
                await webhookEventRepository.SaveAsync(webhookEvent, args.CancellationToken);

                await billingEventService.HandleWebhookAsync(dto, args.CancellationToken);

                webhookEvent.ProcessingStatus = "Processed";
                webhookEvent.CorrelationTenantId = dto.TenantId;
                webhookEvent.ProcessedUtc = DateTime.UtcNow;
                await webhookEventRepository.SaveAsync(webhookEvent, args.CancellationToken);

                await auditService.RecordAsync(new RecordAuditEventRequestDto
                {
                    TenantId = dto.TenantId,
                    Category = "billing",
                    Action = "webhook_processed",
                    Severity = "info",
                    TargetType = "webhook_event",
                    TargetId = dto.EventId,
                    Source = nameof(BillingEventWorker),
                    Description = $"Processed {dto.Provider} webhook {dto.EventType} from queue."
                }, args.CancellationToken);

                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing billing event message {MessageId}.", args.Message.MessageId);

                if (dto is not null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var webhookEventRepository = scope.ServiceProvider.GetRequiredService<IWebhookEventRepository>();
                    var alertService = scope.ServiceProvider.GetRequiredService<IOperationalAlertService>();

                    var partitionKey = dto.Provider.ToUpperInvariant();
                    var webhookEvent = await webhookEventRepository.GetAsync(partitionKey, dto.EventId, args.CancellationToken);
                    if (webhookEvent is not null)
                    {
                        webhookEvent.ProcessingStatus = "Failed";
                        webhookEvent.CorrelationTenantId = dto.TenantId;
                        webhookEvent.ProcessedUtc = DateTime.UtcNow;
                        await webhookEventRepository.SaveAsync(webhookEvent, args.CancellationToken);
                    }

                    await alertService.RaiseAsync(new RaiseOperationalAlertRequestDto
                    {
                        TenantId = dto.TenantId,
                        AlertType = "webhook_failure",
                        Severity = "error",
                        DedupeKey = $"{dto.Provider}:{dto.EventId}:failed",
                        Source = nameof(BillingEventWorker),
                        Message = $"Failed processing {dto.Provider} webhook {dto.EventType} from queue.",
                        ContextJson = dto.PayloadJson
                    }, args.CancellationToken);
                }

                if (args.Message.DeliveryCount >= 5)
                {
                    await args.DeadLetterMessageAsync(args.Message, "BillingEventProcessingFailed", ex.Message, args.CancellationToken);
                    return;
                }

                await args.AbandonMessageAsync(args.Message, cancellationToken: args.CancellationToken);
            }
        }

        private Task HandleErrorAsync(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Service Bus error in {EntityPath}, ErrorSource={ErrorSource}", args.EntityPath, args.ErrorSource);
            return Task.CompletedTask;
        }
    }
}
