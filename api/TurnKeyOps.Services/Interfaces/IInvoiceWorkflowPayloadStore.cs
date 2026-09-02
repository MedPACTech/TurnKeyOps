using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IInvoiceWorkflowPayloadStore
{
    Task<string> SaveAsync(Guid tenantId, Guid invoiceId, InvoiceWorkflowPayloadDto payload, CancellationToken ct = default);
    Task<InvoiceWorkflowPayloadDto> LoadAsync(string? blobName, CancellationToken ct = default);
    Task DeleteIfExistsAsync(string? blobName, CancellationToken ct = default);
}
