using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IChatRepository : IBaseRepositoryAsync<Chat>
    {
        Task<Chat?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<Chat?> GetByRowKeyAsync(string rowKey, CancellationToken ct = default);
        Task<List<Chat>> GetChatsByUserAsync(string partitionKey, CancellationToken ct);
        Task<List<Chat>> GetChatsByUserAndPatientIdAsync(string partitionKey, Guid patientId, CancellationToken ct);
        Task<List<Chat>> GetChatsByTenantAndPatientIdAsync(string tenantPartitionPrefix, Guid patientId, CancellationToken ct);
    }
}
