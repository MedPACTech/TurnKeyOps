using MedInsights.Lib.Configurations;
using MedInsights.Repositories.Interfaces;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using TurnKeyOps.Lib.Dtos;
using TurnKeyOps.Lib.Utils;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public class TurnKeyChatService : ITurnKeyChatService
{
    private readonly IChatRepository _chatRepository;
    private readonly IChatMessageRepository _chatMessageRepository;
    private readonly IUserContext _userContext;
    private readonly OpenAIClient _openAIClient;
    private readonly OpenAISettings _settings;

    private const string SystemPrompt = """
        You are Bob, a friendly and knowledgeable AI assistant for contractors.
        You help with estimating jobs, scheduling, managing customers, and general contracting questions.
        You specialize in concrete and framing trades. Keep answers practical and concise.
        When discussing estimates, use standard industry formulas.
        Concrete: CY = (L×W×depth_in/12)/27, add 5-10% waste.
        Framing: studs = wall_length × 0.75 for 16" OC, plates = length × 3.
        Always be encouraging and professional. These are busy contractors on job sites.
        """;

    public TurnKeyChatService(
        IChatRepository chatRepository,
        IChatMessageRepository chatMessageRepository,
        IUserContext userContext,
        OpenAIClient openAIClient,
        IOptions<OpenAISettings> settings)
    {
        _chatRepository = chatRepository;
        _chatMessageRepository = chatMessageRepository;
        _userContext = userContext;
        _openAIClient = openAIClient;
        _settings = settings.Value;
    }

    public async Task<IEnumerable<TurnKeyChatDto>> GetChatsAsync()
    {
        var chats = await _chatRepository.GetChatsByUserAsync(BuildPartitionKey(), CancellationToken.None);
        return chats.Select(MapChat);
    }

    public async Task<TurnKeyChatDto> CreateChatAsync()
    {
        var now = DateTime.UtcNow;
        var chatId = Guid.NewGuid();
        var chat = new MedInsights.Lib.Entities.Chat
        {
            Id = chatId,
            PartitionKey = BuildPartitionKey(),
            RowKey = MedInsights.Lib.EntityKeyPolicy.Row(chatId),
            Title = "New Chat",
            CustomTitle = string.Empty,
            TokensUsed = 0,
            PatientId = null,
            DateChatCreated = now,
            DateChatUpdated = now,
            ChatSummary = string.Empty,
            ChatMetadata = string.Empty,
            AttachedDocuments = "[]",
            IsDeleted = false
        };

        var saved = await _chatRepository.SaveAsync(chat, CancellationToken.None);
        return MapChat(saved);
    }

    public async Task<IEnumerable<TurnKeyChatMessageDto>> GetMessagesAsync(Guid chatId)
    {
        var messages = await _chatMessageRepository.GetMessagesByChatAsync(BuildPartitionKey(), chatId, 0, CancellationToken.None);
        return messages.Select(m => new TurnKeyChatMessageDto
        {
            Id = m.Id == Guid.Empty ? m.MessageId : m.Id,
            ChatId = chatId,
            Role = m.Role,
            Content = m.Content,
            DateCreated = m.ChatTimestamp
        });
    }

    public async Task<TurnKeyChatMessageDto> SendMessageAsync(Guid chatId, string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("Message is required.", nameof(userMessage));

        await EnsureChatExistsAsync(chatId);

        var userEntity = new MedInsights.Lib.Entities.ChatMessage
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            PartitionKey = BuildPartitionKey(),
            RowKey = MedInsights.Lib.Utils.RepositoryKeyHelper.ToOrderedRowKey(chatId),
            Role = "user",
            Content = userMessage.Trim(),
            ChatTimestamp = DateTime.UtcNow,
            TokensUsed = 0,
            IsDeleted = false
        };
        userEntity.Id = userEntity.MessageId;
        await _chatMessageRepository.SaveAsync(userEntity, CancellationToken.None);

        var history = (await _chatMessageRepository.GetMessagesByChatAsync(BuildPartitionKey(), chatId, 20, CancellationToken.None))
            .OrderBy(m => m.ChatTimestamp)
            .ToList();

        var chatClient = _openAIClient.GetChatClient(_settings.DefaultModel);
        var prompt = new List<OpenAI.Chat.ChatMessage> { new SystemChatMessage(SystemPrompt) };
        foreach (var message in history)
        {
            if (string.Equals(message.Role, "assistant", StringComparison.OrdinalIgnoreCase))
                prompt.Add(new AssistantChatMessage(message.Content));
            else
                prompt.Add(new UserChatMessage(message.Content));
        }

        var completion = await chatClient.CompleteChatAsync(prompt);
        var assistantContent = completion.Value.Content.FirstOrDefault()?.Text ?? string.Empty;

        var assistantEntity = new MedInsights.Lib.Entities.ChatMessage
        {
            Id = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            PartitionKey = BuildPartitionKey(),
            RowKey = MedInsights.Lib.Utils.RepositoryKeyHelper.ToOrderedRowKey(chatId),
            Role = "assistant",
            Content = assistantContent,
            ChatTimestamp = DateTime.UtcNow,
            TokensUsed = 0,
            IsDeleted = false
        };
        assistantEntity.Id = assistantEntity.MessageId;
        await _chatMessageRepository.SaveAsync(assistantEntity, CancellationToken.None);

        var chat = await EnsureChatExistsAsync(chatId);
        chat.Title = chat.Title == "New Chat"
            ? BuildTitle(userMessage)
            : chat.Title;
        chat.DateChatUpdated = DateTime.UtcNow;
        chat.TokensUsed += history.Count + 2;
        await _chatRepository.SaveAsync(chat, CancellationToken.None);

        return new TurnKeyChatMessageDto
        {
            Id = assistantEntity.Id,
            ChatId = chatId,
            Role = assistantEntity.Role,
            Content = assistantEntity.Content,
            DateCreated = assistantEntity.ChatTimestamp
        };
    }

    public async Task DeleteChatAsync(Guid chatId)
    {
        var chat = await _chatRepository.GetByRowKeyAsync(MedInsights.Lib.EntityKeyPolicy.Row(chatId), CancellationToken.None);
        if (chat is null)
            return;

        chat.IsDeleted = true;
        chat.DateChatUpdated = DateTime.UtcNow;
        await _chatRepository.SaveAsync(chat, CancellationToken.None);
        await _chatMessageRepository.DeleteMessagesByChatAsync(BuildPartitionKey(), chatId, CancellationToken.None);
    }

    private string BuildPartitionKey() =>
        MedInsights.Lib.EntityKeyPolicy.TenantUserPartition(_userContext.TenantId, _userContext.UserId);

    private async Task<MedInsights.Lib.Entities.Chat> EnsureChatExistsAsync(Guid chatId)
    {
        var chat = await _chatRepository.GetAsync(BuildPartitionKey(), MedInsights.Lib.EntityKeyPolicy.Row(chatId), CancellationToken.None);
        if (chat is null)
            throw new KeyNotFoundException("Chat not found.");

        return chat;
    }

    private static string BuildTitle(string userMessage)
    {
        var trimmed = userMessage.Trim();
        if (trimmed.Length <= 50)
            return trimmed;

        return $"{trimmed[..50]}...";
    }

    private static TurnKeyChatDto MapChat(MedInsights.Lib.Entities.Chat chat) => new()
    {
        Id = chat.Id == Guid.Empty ? MedInsights.Lib.Utils.RepositoryKeyHelper.FromRowKey(chat.RowKey) : chat.Id,
        Title = string.IsNullOrWhiteSpace(chat.Title) ? "New Chat" : chat.Title,
        MessageCount = Math.Max(chat.TokensUsed, 0),
        DateCreated = chat.DateChatCreated,
        DateUpdated = chat.DateChatUpdated
    };
}
