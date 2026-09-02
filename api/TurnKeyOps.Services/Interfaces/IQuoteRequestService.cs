using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IQuoteRequestService
{
    Task<IReadOnlyCollection<QuoteRequestDto>> ListAsync(CancellationToken ct = default);
    Task<QuoteRequestDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<QuoteRequestDto> CreatePublicAsync(
        string tenantSlug,
        CreateQuoteRequestDto dto,
        CancellationToken ct = default);
    Task<QuoteRequestDto?> UpdateAsync(Guid id, QuoteRequestDto dto, CancellationToken ct = default);
}
