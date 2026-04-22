// MedInsights.Services/IChatService.cs
using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces;

public interface IChatService
{
    Task<List<ChatDto>> GetChatsAsync(CancellationToken ct = default);
    Task<ChatDto> GetChatByIdAsync(Guid chatId, CancellationToken ct = default);
    Task<ChatDto> GetOrCreateChatAsync(Guid chatId, CancellationToken ct = default);
    Task<ChatDto> UpdateChatTitleAsync(Guid chatId, string title, bool isCustomTitle, CancellationToken ct = default);
    Task<ChatDto> UpdateChatSummaryAsync(Guid chatId, string summary, CancellationToken ct = default);
    Task<ChatDto> UpdateAttachedDocumentsAsync(Guid chatId, List<Guid> attachedDocuments, CancellationToken ct = default);
    Task<bool> DeleteChatAsync(Guid chatId, CancellationToken ct = default);

    //Chat Messages
    Task<List<ChatMessageDto>> GetChatMessagesAsync(Guid chatId, int limit = 0, CancellationToken ct = default);
    Task<ChatMessageDto> SaveChatMessageAsync(ChatMessageDto chatMessageDto, CancellationToken ct = default);
    Task<ChatDto> CreateChatAsync(Guid? patientId, CancellationToken ct = default);

    //Patient Chats
    Task<List<ChatDto>> GetChatsForCurrentProviderByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<List<ChatDto>> GetChatsForTenantByPatientIdAsync(Guid patientId, CancellationToken ct = default);    
}
