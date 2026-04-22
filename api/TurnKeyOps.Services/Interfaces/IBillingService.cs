using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IBillingService
    {
        Task<PaymentCheckoutSessionDto> CreateSubscriptionCheckoutAsync(CreateSubscriptionCheckoutRequestDto dto, CancellationToken ct = default);
        Task<PaymentPortalSessionDto> CreateCustomerPortalAsync(CreateCustomerPortalRequestDto dto, CancellationToken ct = default);
        Task<PaymentSubscriptionResultDto> UpdateSubscriptionSeatsAsync(UpdateSubscriptionSeatsRequestDto dto, CancellationToken ct = default);
        Task<PaymentSubscriptionResultDto> ScheduleSeatReductionAsync(ScheduleSeatReductionRequestDto dto, CancellationToken ct = default);
        Task<PaymentCheckoutSessionDto> PurchaseCreditTopUpAsync(PurchaseCreditTopUpRequestDto dto, CancellationToken ct = default);
        Task<PaymentSubscriptionResultDto> CancelAtTermEndAsync(CancelSubscriptionRequestDto dto, CancellationToken ct = default);
        Task<PaymentSubscriptionResultDto> ReactivateAsync(ReactivateSubscriptionRequestDto dto, CancellationToken ct = default);
    }
}
