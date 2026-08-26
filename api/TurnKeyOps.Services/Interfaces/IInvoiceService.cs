using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IInvoiceService
{
    Task<InvoiceDto?> GetAsync(Guid id);
    Task<(IEnumerable<InvoiceDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken);
    Task<InvoiceDto> AddAsync(InvoiceDto dto);
    Task<InvoiceDto> UpdateAsync(InvoiceDto dto);
    Task<InvoiceDto> CreateFromEstimateAsync(Guid estimateId);
    Task<IReadOnlyCollection<InvoiceDto>> SyncApprovedEstimatesAsync(CancellationToken ct = default);
    Task<InvoiceDto> SendAsync(Guid id, string? expectedVersion, CancellationToken ct = default);
    Task<InvoiceDto> RecordPaymentAsync(Guid id, InvoicePaymentInputDto input, CancellationToken ct = default);
    Task<InvoiceDto> RecordRefundAsync(Guid id, InvoicePaymentInputDto input, CancellationToken ct = default);
    Task<InvoiceDto> RecordReminderAsync(Guid id, InvoiceReminderInputDto input, CancellationToken ct = default);
    Task<InvoiceJobReleaseDto> GetJobReleaseAsync(Guid id, CancellationToken ct = default);
    Task<InvoiceDto> ReconcileProviderEventAsync(Guid tenantId, Guid id, InvoicePaymentInputDto input, CancellationToken ct = default);
    Task DeleteAsync(Guid id);
}
