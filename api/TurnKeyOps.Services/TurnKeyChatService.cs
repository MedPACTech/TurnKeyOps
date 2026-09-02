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
    private readonly MedInsights.Services.Interfaces.IRoleAccessService _roleAccess;

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
        IOptions<OpenAISettings> settings,
        MedInsights.Services.Interfaces.IRoleAccessService roleAccess)
    {
        _chatRepository = chatRepository;
        _chatMessageRepository = chatMessageRepository;
        _userContext = userContext;
        _openAIClient = openAIClient;
        _settings = settings.Value;
        _roleAccess = roleAccess;
    }

    public async Task<IEnumerable<TurnKeyChatDto>> GetChatsAsync()
    {
        await _roleAccess.RequirePermissionAsync(MedInsights.Lib.Authorization.TurnKeyPermissionKeys.OperationsRead);
        var chats = await _chatRepository.GetChatsByUserAsync(BuildPartitionKey(), CancellationToken.None);
        return chats.Select(MapChat);
    }

    public async Task<TurnKeyChatDto> CreateChatAsync()
        => await CreateChatAsync(new CreateTurnKeyChatDto(), CancellationToken.None);

    public async Task<TurnKeyChatDto> CreateChatAsync(CreateTurnKeyChatDto input, CancellationToken ct = default)
    {
        await _roleAccess.RequirePermissionAsync(MedInsights.Lib.Authorization.TurnKeyPermissionKeys.OperationsManage, ct);
        var now = DateTime.UtcNow;
        var chatId = input.Id.GetValueOrDefault();
        if (chatId == Guid.Empty) chatId = Guid.NewGuid();
        var existing = await _chatRepository.GetAsync(
            BuildPartitionKey(),
            MedInsights.Lib.EntityKeyPolicy.Row(chatId),
            ct);
        if (existing is not null) return MapChat(existing);
        var chat = new MedInsights.Lib.Entities.Chat
        {
            Id = chatId,
            PartitionKey = BuildPartitionKey(),
            RowKey = MedInsights.Lib.EntityKeyPolicy.Row(chatId),
            TenantId = _userContext.TenantId,
            ActorUserId = _userContext.UserId,
            Title = CleanTitle(input.Title),
            Mode = NormalizeMode(input.Mode),
            StateJson = NormalizeJson(input.StateJson),
            CustomTitle = string.Empty,
            TokensUsed = 0,
            DateChatCreated = now,
            DateChatUpdated = now,
            ChatSummary = string.Empty,
            ChatMetadata = string.Empty,
            AttachedDocuments = "[]",
            IsDeleted = false
        };

        var saved = await _chatRepository.SaveAsync(chat, ct);
        return MapChat(saved);
    }

    public async Task<TurnKeyChatDto> UpdateChatAsync(
        Guid chatId,
        UpdateTurnKeyChatDto input,
        CancellationToken ct = default)
    {
        await _roleAccess.RequirePermissionAsync(MedInsights.Lib.Authorization.TurnKeyPermissionKeys.OperationsManage, ct);
        var chat = await EnsureChatExistsAsync(chatId, ct);
        chat.Title = CleanTitle(input.Title);
        chat.Mode = NormalizeMode(input.Mode);
        chat.StateJson = NormalizeJson(input.StateJson);
        chat.ArchivedAtUtc = input.Archived ? chat.ArchivedAtUtc ?? DateTime.UtcNow : null;
        chat.DateChatUpdated = DateTime.UtcNow;
        return MapChat(await _chatRepository.SaveAsync(chat, ct));
    }

    public async Task<IEnumerable<TurnKeyChatMessageDto>> GetMessagesAsync(Guid chatId)
    {
        await _roleAccess.RequirePermissionAsync(MedInsights.Lib.Authorization.TurnKeyPermissionKeys.OperationsRead);
        await EnsureChatExistsAsync(chatId);
        var messages = await _chatMessageRepository.GetMessagesByChatAsync(BuildPartitionKey(), chatId, 0, CancellationToken.None);
        return messages.Select(message => MapMessage(message, chatId));
    }

    public async Task<TurnKeyChatMessageDto> AppendMessageAsync(
        Guid chatId,
        AppendTurnKeyChatMessageDto input,
        CancellationToken ct = default)
    {
        await _roleAccess.RequirePermissionAsync(MedInsights.Lib.Authorization.TurnKeyPermissionKeys.OperationsManage, ct);
        if (string.IsNullOrWhiteSpace(input.Content))
            throw new ArgumentException("Message content is required.", nameof(input.Content));
        var role = input.Role.Trim().ToLowerInvariant();
        if (role is not "user" and not "assistant" and not "system")
            throw new ArgumentException("Message role is invalid.", nameof(input.Role));
        var idempotencyKey = input.IdempotencyKey.Trim();
        if (idempotencyKey.Length is 0 or > 128)
            throw new ArgumentException("A stable idempotency key of 1-128 characters is required.", nameof(input.IdempotencyKey));

        var chat = await EnsureChatExistsAsync(chatId, ct);
        var existing = (await _chatMessageRepository.GetMessagesByChatAsync(BuildPartitionKey(), chatId, 0, ct))
            .FirstOrDefault(message => string.Equals(message.IdempotencyKey, idempotencyKey, StringComparison.Ordinal));
        if (existing is not null) return MapMessage(existing, chatId);

        var messageId = Guid.NewGuid();
        var entity = new MedInsights.Lib.Entities.ChatMessage
        {
            Id = messageId,
            MessageId = messageId,
            PartitionKey = BuildPartitionKey(),
            RowKey = BuildMessageRowKey(chatId, messageId),
            TenantId = _userContext.TenantId,
            ActorUserId = _userContext.UserId,
            ChatId = chatId,
            Role = role,
            Content = input.Content.Trim(),
            MetadataJson = NormalizeJson(input.MetadataJson),
            IdempotencyKey = idempotencyKey,
            ChatTimestamp = DateTime.UtcNow,
            IsDeleted = false
        };
        var saved = await _chatMessageRepository.SaveAsync(entity, ct);
        chat.DateChatUpdated = DateTime.UtcNow;
        chat.TokensUsed += 1;
        await _chatRepository.SaveAsync(chat, ct);
        return MapMessage(saved, chatId);
    }

    public async Task<TurnKeyChatMessageDto> SendMessageAsync(Guid chatId, string userMessage)
    {
        await _roleAccess.RequirePermissionAsync(MedInsights.Lib.Authorization.TurnKeyPermissionKeys.OperationsManage);
        if (string.IsNullOrWhiteSpace(userMessage))
            throw new ArgumentException("Message is required.", nameof(userMessage));

        await EnsureChatExistsAsync(chatId);

        var userMessageId = Guid.NewGuid();
        var userEntity = new MedInsights.Lib.Entities.ChatMessage
        {
            Id = userMessageId,
            MessageId = userMessageId,
            PartitionKey = BuildPartitionKey(),
            RowKey = BuildMessageRowKey(chatId, userMessageId),
            TenantId = _userContext.TenantId,
            ActorUserId = _userContext.UserId,
            ChatId = chatId,
            Role = "user",
            Content = userMessage.Trim(),
            ChatTimestamp = DateTime.UtcNow,
            TokensUsed = 0,
            IsDeleted = false
        };
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

        var assistantMessageId = Guid.NewGuid();
        var assistantEntity = new MedInsights.Lib.Entities.ChatMessage
        {
            Id = assistantMessageId,
            MessageId = assistantMessageId,
            PartitionKey = BuildPartitionKey(),
            RowKey = BuildMessageRowKey(chatId, assistantMessageId),
            TenantId = _userContext.TenantId,
            ActorUserId = _userContext.UserId,
            ChatId = chatId,
            Role = "assistant",
            Content = assistantContent,
            ChatTimestamp = DateTime.UtcNow,
            TokensUsed = 0,
            IsDeleted = false
        };
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
        await _roleAccess.RequirePermissionAsync(MedInsights.Lib.Authorization.TurnKeyPermissionKeys.OperationsManage);
        var chat = await _chatRepository.GetAsync(
            BuildPartitionKey(),
            MedInsights.Lib.EntityKeyPolicy.Row(chatId),
            CancellationToken.None);
        if (chat is null)
            return;

        chat.IsDeleted = true;
        chat.DateChatUpdated = DateTime.UtcNow;
        await _chatRepository.SaveAsync(chat, CancellationToken.None);
        await _chatMessageRepository.DeleteMessagesByChatAsync(BuildPartitionKey(), chatId, CancellationToken.None);
    }

    private string BuildPartitionKey() =>
        MedInsights.Lib.EntityKeyPolicy.TenantUserPartition(_userContext.TenantId, _userContext.UserId);

    private async Task<MedInsights.Lib.Entities.Chat> EnsureChatExistsAsync(
        Guid chatId,
        CancellationToken ct = default)
    {
        var chat = await _chatRepository.GetAsync(BuildPartitionKey(), MedInsights.Lib.EntityKeyPolicy.Row(chatId), ct);
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
        Mode = string.IsNullOrWhiteSpace(chat.Mode) ? "general" : chat.Mode,
        StateJson = string.IsNullOrWhiteSpace(chat.StateJson) ? "{}" : chat.StateJson,
        MessageCount = Math.Max(chat.TokensUsed, 0),
        DateCreated = chat.DateChatCreated,
        DateUpdated = chat.DateChatUpdated,
        ArchivedAtUtc = chat.ArchivedAtUtc
    };

    private static TurnKeyChatMessageDto MapMessage(MedInsights.Lib.Entities.ChatMessage message, Guid chatId) => new()
    {
        Id = message.Id == Guid.Empty ? message.MessageId : message.Id,
        ChatId = chatId,
        Role = message.Role,
        Content = message.Content,
        MetadataJson = string.IsNullOrWhiteSpace(message.MetadataJson) ? "{}" : message.MetadataJson,
        IdempotencyKey = message.IdempotencyKey,
        DateCreated = message.ChatTimestamp
    };

    private static string CleanTitle(string? value)
    {
        var title = string.IsNullOrWhiteSpace(value) ? "New conversation" : value.Trim();
        return title.Length <= 100 ? title : title[..100];
    }

    private static string NormalizeMode(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "estimate-builder" => "estimate-builder",
        "estimate-followup" => "estimate-followup",
        _ => "general"
    };

    private static string NormalizeJson(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "{}";
        using var document = System.Text.Json.JsonDocument.Parse(value);
        return document.RootElement.GetRawText();
    }

    private static string BuildMessageRowKey(Guid chatId, Guid messageId) =>
        $"{MedInsights.Lib.Utils.RepositoryKeyHelper.ToOrderedRowKey(chatId)}|{messageId:N}";
}
