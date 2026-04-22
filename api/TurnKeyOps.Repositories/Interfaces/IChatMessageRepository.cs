using IBeam.Repositories.Abstractions;
using MedInsights.Lib.Entities;

namespace MedInsights.Repositories.Interfaces
{
    public interface IChatMessageRepository : IBaseRepositoryAsync<ChatMessage>
    {
        Task<List<ChatMessage>> GetMessagesByChatAsync(string partitionKey, Guid chatId, int limit, CancellationToken ct = default);
        Task DeleteMessagesByChatAsync(string partitionKey, Guid chatId, CancellationToken ct = default);
    }
}
