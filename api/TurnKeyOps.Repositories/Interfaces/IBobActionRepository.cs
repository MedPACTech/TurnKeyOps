using IBeam.Repositories.Abstractions;
using TurnKeyOps.Lib.Entities;

namespace TurnKeyOps.Repositories.Interfaces;

public interface IBobActionRepository : IBaseRepositoryAsync<BobActionRecord>
{
    Task<BobActionRecord?> GetAsync(string partitionKey, Guid actionId, CancellationToken ct = default);
    Task<BobActionRecord?> FindByIdempotencyKeyAsync(
        string partitionKey,
        string idempotencyKey,
        CancellationToken ct = default);
    Task<IReadOnlyList<BobActionRecord>> ListByConversationAsync(
        string partitionKey,
        Guid conversationId,
        CancellationToken ct = default);
}
