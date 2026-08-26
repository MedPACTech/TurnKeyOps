using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IInvoiceWebhookService
{
    Task<InvoiceProviderWebhookResultDto> ReceiveAsync(
        string provider,
        string json,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken ct = default);
}
