using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IDocumentRepository : IBaseRepositoryAsync<Document>
    {
        Task<Document?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<IEnumerable<Document>> GetDocumentsByUserIdAsync(Guid userId, CancellationToken ct);
        Task<IEnumerable<Document>> GetDocumentsByChatIdAsync(Guid chatId, CancellationToken ct);
        Task<IEnumerable<Document>> GetDocumentsByIdsAsync(IList<Guid> documentIds, CancellationToken ct);
        Task<IEnumerable<Document>> GetDocumentsByPatientIdAsync(Guid patientId, CancellationToken ct);
    }
}
