using Azure.Messaging.ServiceBus;
using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedInsights.Services.BackgroundServices
{
    public class TokenTransactionWorker : BackgroundService
    {
        private readonly ServiceBusProcessor _processor;
        private readonly ILogger<TokenTransactionWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public TokenTransactionWorker(
            ServiceBusClient sbClient,
            ILogger<TokenTransactionWorker> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;

            _processor = sbClient.CreateProcessor("token-transactions", new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 2,
                AutoCompleteMessages = false
            });
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _processor.ProcessMessageAsync += HandleMessageAsync;
            _processor.ProcessErrorAsync += ErrorHandler;

            _logger.LogInformation("TokenTransactionWorker started, listening on 'token-transactions' queue...");
            await _processor.StartProcessingAsync(stoppingToken);
        }

        private async Task HandleMessageAsync(ProcessMessageEventArgs args)
        {
            string? messageId = args.Message.MessageId;
            string? correlationId = args.Message.CorrelationId;

            _logger.LogInformation(">>> START MessageId={MessageId}", args.Message.MessageId);

            try
            {
                var transaction = args.Message.Body.ToObjectFromJson<TokenTransactionMessage>();

                if (transaction == null)
                {
                    _logger.LogError("Failed to deserialize TokenTransactionMessage. MessageId={MessageId}, dead-lettering.", messageId);
                    await args.DeadLetterMessageAsync(args.Message);
                    return;
                }

                _logger.LogInformation(
                    "Processing MessageId={MessageId}, CorrelationId={CorrelationId}, Tenant={TenantId}, User={UserId}, Credit={Credit}, Debit={Debit}",
                    messageId,
                    correlationId ?? "(none)",
                    transaction.TenantId,
                    transaction.UserId,
                    transaction.TokensCredited,
                    transaction.TokensDebited);

                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITokenLedgerRepository>();

                var tenantId = EntityKeyPolicy.TenantPartition(transaction.TenantId);
                var last = await repository.GetLatestByTenantAsync(tenantId);
                var currentBalance = last?.BalanceAfterTransaction ?? 0;
                var newBalance = currentBalance + transaction.TokensCredited - transaction.TokensDebited;

                var ledgerEntity = new TokenLedger
                {
                    Id = Guid.NewGuid(),
                    PartitionKey = tenantId,
                    RowKey = string.Empty,
                    UserId = transaction.UserId,
                    Date = DateTime.UtcNow,
                    TokenType = transaction.TokenType,
                    TokensCredited = transaction.TokensCredited,
                    TokensDebited = transaction.TokensDebited,
                    Description = transaction.Description,
                    BalanceAfterTransaction = newBalance,
                    IsDeleted = false
                };

                ledgerEntity.RowKey = RepositoryKeyHelper.ToOrderedRowKey(ledgerEntity.Id);

                _logger.LogInformation("Inserting MessageId={MessageId}...", args.Message.MessageId);
                await repository.SaveAsync(ledgerEntity);
                _logger.LogInformation("Inserted MessageId={MessageId} into TokenLedger", args.Message.MessageId);

                _logger.LogInformation(
                    "Persisted MessageId={MessageId} for Tenant={TenantId}, New Balance={Balance}",
                    messageId,
                    transaction.TenantId,
                    newBalance);

                _logger.LogInformation("Completing MessageId={MessageId}", args.Message.MessageId);
                await args.CompleteMessageAsync(args.Message);
                _logger.LogInformation("Completed MessageId={MessageId}", args.Message.MessageId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling MessageId={MessageId}, CorrelationId={CorrelationId}. Abandoning...", messageId, correlationId);
                await args.AbandonMessageAsync(args.Message);
            }
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Service Bus error in {EntityPath}, ErrorSource={ErrorSource}", args.EntityPath, args.ErrorSource);
            return Task.CompletedTask;
        }
    }
}

