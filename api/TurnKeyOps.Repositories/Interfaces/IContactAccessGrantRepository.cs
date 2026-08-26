using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IContactAccessGrantRepository : IBaseRepositoryAsync<ContactAccessGrant>
{
    Task<ContactAccessGrant?> GetAsync(
        string partitionKey,
        string rowKey,
        CancellationToken ct = default,
        bool includeDeleted = false);

    Task<IReadOnlyList<ContactAccessGrant>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);
}
