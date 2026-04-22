using TurnKeyOps.Lib.Dtos;

namespace TurnKeyOps.Services.Interfaces;

public interface ITurnKeyChatService
{
    Task<IEnumerable<TurnKeyChatDto>> GetChatsAsync();
    Task<TurnKeyChatDto> CreateChatAsync();
    Task<IEnumerable<TurnKeyChatMessageDto>> GetMessagesAsync(Guid chatId);
    Task<TurnKeyChatMessageDto> SendMessageAsync(Guid chatId, string userMessage);
    Task DeleteChatAsync(Guid chatId);
}
