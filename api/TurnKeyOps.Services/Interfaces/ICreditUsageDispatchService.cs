using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface ICreditUsageDispatchService
    {
        Task<Guid> EnqueueAsync(CreditUsageMessageDto dto, CancellationToken ct = default);
    }
}
