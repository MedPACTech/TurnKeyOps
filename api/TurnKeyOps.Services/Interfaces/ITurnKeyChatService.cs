using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface ITurnKeyChatService
{
    Task<IEnumerable<TurnKeyChatDto>> GetChatsAsync();
    Task<TurnKeyChatDto> CreateChatAsync();
    Task<TurnKeyChatDto> CreateChatAsync(CreateTurnKeyChatDto input, CancellationToken ct = default);
    Task<TurnKeyChatDto> UpdateChatAsync(Guid chatId, UpdateTurnKeyChatDto input, CancellationToken ct = default);
    Task<IEnumerable<TurnKeyChatMessageDto>> GetMessagesAsync(Guid chatId);
    Task<TurnKeyChatMessageDto> AppendMessageAsync(
        Guid chatId,
        AppendTurnKeyChatMessageDto input,
        CancellationToken ct = default);
    Task<TurnKeyChatMessageDto> SendMessageAsync(Guid chatId, string userMessage);
    Task DeleteChatAsync(Guid chatId);
}
