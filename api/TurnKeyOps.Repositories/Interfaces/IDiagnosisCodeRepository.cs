using IBeam.Repositories.Abstractions;
using IBeam.Repositories.AzureTables;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IDiagnosisCodeRepository : IAzureTablesRepositoryAsync<DiagnosisCode>
    {
        Task<IReadOnlyList<DiagnosisCode>> GetAllAsync(CancellationToken ct = default);
    }
}
