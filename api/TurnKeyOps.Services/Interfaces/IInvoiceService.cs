using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IInvoiceService
{
    Task<InvoiceDto?> GetAsync(Guid id);
    Task<(IEnumerable<InvoiceDto> Items, string? ContinuationToken)> GetPagedAsync(int pageSize, string? continuationToken);
    Task<InvoiceDto> AddAsync(InvoiceDto dto);
    Task<InvoiceDto> UpdateAsync(InvoiceDto dto);
    Task<InvoiceDto> CreateFromEstimateAsync(Guid estimateId);
    Task DeleteAsync(Guid id);
}
