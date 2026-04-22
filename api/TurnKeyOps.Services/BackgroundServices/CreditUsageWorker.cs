using Azure.Messaging.ServiceBus;
using MedInsights.Lib.Dtos;
using MedInsights.Repositories.Interfaces;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedInsights.Services.BackgroundServices
{
    public sealed class CreditUsageWorker : BackgroundService
    {
        private const string QueueName = "credit-usage";
        private readonly ServiceBusProcessor _processor;
        private readonly ILogger<CreditUsageWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public CreditUsageWorker(
            ServiceBusClient serviceBusClient,
            ILogger<CreditUsageWorker> logger,
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

            _logger.LogInformation("CreditUsageWorker started, listening on '{QueueName}' queue.", QueueName);
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
            CreditUsageMessageDto? dto = null;

            try
            {
                dto = args.Message.Body.ToObjectFromJson<CreditUsageMessageDto>();
                if (dto is null || dto.RequestId == Guid.Empty)
                {
                    await args.DeadLetterMessageAsync(args.Message, cancellationToken: args.CancellationToken);
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var processingRepository = scope.ServiceProvider.GetRequiredService<IProcessingCreditUsageRepository>();
                var creditAccountingService = scope.ServiceProvider.GetRequiredService<ICreditAccountingService>();
                var alertService = scope.ServiceProvider.GetRequiredService<IOperationalAlertService>();

                var request = await processingRepository.GetByRequestIdAsync(dto.RequestId, args.CancellationToken);
                if (request is null)
                {
                    await args.DeadLetterMessageAsync(args.Message, "CreditUsageRequestNotFound", "No persisted credit usage request was found.", args.CancellationToken);
                    return;
                }

                if (request.Completed)
                {
                    await args.CompleteMessageAsync(args.Message, args.CancellationToken);
                    return;
                }

                await creditAccountingService.ConsumeCreditsDirectAsync(
                    dto.TenantId,
                    dto.UserId,
                    dto.UsagePeriodKey,
                    dto.Credits,
                    dto.SourceReference,
                    dto.Description,
                    dto.EffectiveUtc,
                    args.CancellationToken);

                request.Completed = true;
                request.CompletedUtc = DateTime.UtcNow;
                request.LastError = null;
                await processingRepository.SaveAsync(request, args.CancellationToken);

                await args.CompleteMessageAsync(args.Message, args.CancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed processing credit usage message {MessageId}.", args.Message.MessageId);

                if (dto is not null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var processingRepository = scope.ServiceProvider.GetRequiredService<IProcessingCreditUsageRepository>();
                    var alertService = scope.ServiceProvider.GetRequiredService<IOperationalAlertService>();

                    var request = await processingRepository.GetByRequestIdAsync(dto.RequestId, args.CancellationToken);
                    if (request is not null)
                    {
                        request.LastError = ex.Message;
                        await processingRepository.SaveAsync(request, args.CancellationToken);
                    }

                    await alertService.RaiseAsync(new RaiseOperationalAlertRequestDto
                    {
                        TenantId = dto.TenantId,
                        AlertType = "credit_usage_failure",
                        Severity = "error",
                        DedupeKey = $"credit-usage:{dto.RequestId:D}:failed",
                        Source = nameof(CreditUsageWorker),
                        Message = $"Failed processing credit usage request '{dto.RequestId:D}'.",
                        ContextJson = ex.ToString()
                    }, args.CancellationToken);
                }

                if (args.Message.DeliveryCount >= 5)
                {
                    await args.DeadLetterMessageAsync(args.Message, "CreditUsageProcessingFailed", ex.Message, args.CancellationToken);
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
