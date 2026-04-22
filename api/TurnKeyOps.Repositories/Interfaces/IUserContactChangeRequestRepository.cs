using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IUserContactChangeRequestRepository : IBaseRepositoryAsync<UserContactChangeRequest>
    {
        Task<UserContactChangeRequest?> GetAsync(string partitionKey, string rowKey, CancellationToken ct = default, bool includeDeleted = false);
        Task<UserContactChangeRequest?> GetLatestPendingAsync(Guid userId, string channel, CancellationToken ct = default);
    }
}
