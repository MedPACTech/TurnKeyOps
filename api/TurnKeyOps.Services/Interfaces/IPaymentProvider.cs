using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IPaymentProvider
    {
        string ProviderName { get; }
        bool CanHandleWebhooks { get; }

        Task<PaymentCheckoutSessionDto> CreateSubscriptionCheckoutAsync(CreateSubscriptionCheckoutRequestDto dto, CancellationToken ct = default);
        Task<PaymentPortalSessionDto> CreateCustomerPortalAsync(CreateCustomerPortalRequestDto dto, CancellationToken ct = default);
        Task<PaymentSubscriptionResultDto> UpdateSubscriptionSeatsAsync(UpdateSubscriptionSeatsRequestDto dto, CancellationToken ct = default);
        Task<PaymentCheckoutSessionDto> PurchaseCreditTopUpAsync(PurchaseCreditTopUpRequestDto dto, CancellationToken ct = default);
        Task<PaymentTopUpResultDto> PurchaseCreditTopUpAutomaticallyAsync(AutoTopUpChargeRequestDto dto, CancellationToken ct = default);
        Task<PaymentSubscriptionResultDto> CancelAtTermEndAsync(CancelSubscriptionRequestDto dto, CancellationToken ct = default);
        Task<PaymentSubscriptionResultDto> ReactivateAsync(ReactivateSubscriptionRequestDto dto, CancellationToken ct = default);
        Task<PaymentWebhookEventDto> ParseWebhookAsync(string json, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);
        bool TryGetTopUpPriceAmount(string priceKey, out decimal amount);
        bool TryGetTopUpCreditAmount(string priceKey, out int amount);
    }
}
