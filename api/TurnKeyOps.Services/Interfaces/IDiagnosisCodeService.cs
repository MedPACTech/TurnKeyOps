using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IDiagnosisCodeService
    {
        Task<IReadOnlyList<DiagnosisCodeDto>> SearchAsync(string? searchInput, int limit = 50, CancellationToken ct = default);
        Task<DiagnosisCodeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task WarmCacheAsync(CancellationToken ct = default);
    }
}
