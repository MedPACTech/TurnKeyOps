using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface IQuoteEstimateService
{
    Task<IReadOnlyCollection<QuoteEstimateDto>> ListAsync(CancellationToken ct = default);
    Task<QuoteEstimateDto?> GetAsync(Guid quoteRequestId, CancellationToken ct = default);
    Task<QuoteEstimateDto> SaveDraftAsync(Guid quoteRequestId, QuoteEstimateDraftInputDto input, CancellationToken ct = default);
    Task<QuoteEstimateDto> CreateRevisionAsync(Guid quoteRequestId, string? expectedVersion, CancellationToken ct = default);
    Task<QuoteEstimateDto> SendAsync(Guid quoteRequestId, string? expectedVersion, string reviewBasePath, CancellationToken ct = default);
    Task<QuoteEstimateDto?> GetPublicAsync(string tenantSlug, Guid quoteRequestId, string accessToken, CancellationToken ct = default);
    Task<QuoteEstimateDto?> ApproveAsync(string tenantSlug, Guid quoteRequestId, QuoteEstimateDecisionDto decision, CancellationToken ct = default);
    Task<QuoteEstimateDto?> RequestChangesAsync(string tenantSlug, Guid quoteRequestId, QuoteEstimateDecisionDto decision, CancellationToken ct = default);
}
