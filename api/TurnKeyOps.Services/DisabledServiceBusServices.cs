using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    internal static class ServiceBusDisabledMessage
    {
        public const string Text =
            "Azure Service Bus is not configured for this environment. Set ConnectionStrings:AzureServiceBus to enable queue-backed features.";
    }

    public sealed class DisabledBillingEventDispatchService : IBillingEventDispatchService
    {
        public Task EnqueueAsync(PaymentWebhookEventDto dto, CancellationToken ct = default)
            => throw new InvalidOperationException(ServiceBusDisabledMessage.Text);
    }

    public sealed class DisabledCreditUsageDispatchService : ICreditUsageDispatchService
    {
        public Task<Guid> EnqueueAsync(CreditUsageMessageDto dto, CancellationToken ct = default)
            => throw new InvalidOperationException(ServiceBusDisabledMessage.Text);
    }

    public sealed class DisabledTokenLedgerService : ITokenLedgerService
    {
        public Task<TokenLedgerDto> AddTransactionAsync(TokenLedgerDto dto)
            => throw new InvalidOperationException(ServiceBusDisabledMessage.Text);

        public Task<IEnumerable<TokenLedgerDto>> GetAllTransactionsAsync()
            => Task.FromResult<IEnumerable<TokenLedgerDto>>(Array.Empty<TokenLedgerDto>());

        public Task<IEnumerable<TokenLedgerDto>> GetTransactionsByUserAsync(Guid userId)
            => Task.FromResult<IEnumerable<TokenLedgerDto>>(Array.Empty<TokenLedgerDto>());

        public Task<int> GetBalanceAsync()
            => Task.FromResult(0);
    }
}
