using MedInsights.Lib.Entities;
using MedInsights.Services.Mappers;
using MedInsights.Lib.Utils;
using MedInsights.Repositories.Interfaces;
using System.Text.Json;
using MedInsights.Services.BackgroundServices;
using MedInsights.Lib.Dtos;
using MedInsights.Services.Interfaces;

namespace MedInsights.Services
{
    public sealed class ChatService : IChatService
    {
        private readonly IChatRepository _chatRepository;
        private readonly IChatMessageRepository _chatMessageRepository;
        private readonly IUserContext _userContext;

        public ChatService(
            IChatRepository chatRepository,
            IChatMessageRepository chatMessageRepository,
            IUserContext userContext)
        {
            _chatRepository = chatRepository;
            _chatMessageRepository = chatMessageRepository;
            _userContext = userContext;
        }

        private string BuildTenantUserPartitionKey()
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            return EntityKeyPolicy.TenantUserPartition(_userContext.TenantId, _userContext.UserId);
        }

        public async Task<List<ChatDto>> GetChatsAsync(CancellationToken ct)
        {
            var partitionKey = BuildTenantUserPartitionKey();
            var chats = await _chatRepository.GetChatsByUserAsync(partitionKey, ct);

            return chats
                .Select(c => ChatMapper.ToDto(c, c.Id == Guid.Empty ? RepositoryKeyHelper.FromRowKey(c.RowKey) : c.Id, c.CustomTitle))
                .OrderByDescending(c => c.UpdatedUtc)
                .ToList();
        }

        public async Task<ChatDto> GetChatByIdAsync(Guid chatId, CancellationToken ct)
        {
            var partitionKey = BuildTenantUserPartitionKey();
            if (chatId == Guid.Empty)
                throw new ArgumentException("ChatId is required.", nameof(chatId));

            var chat = await _chatRepository.GetAsync(partitionKey, EntityKeyPolicy.Row(chatId), ct);
            if (chat == null)
                throw new KeyNotFoundException("Chat not found.");

            return ChatMapper.ToDto(chat, chatId);
        }

        public async Task<ChatDto> CreateChatAsync(Guid? patientId, CancellationToken ct)
        {
            var partitionKey = BuildTenantUserPartitionKey();
            var nowUtc = DateTime.UtcNow;
            var chatId = Guid.NewGuid();

            var chat = new Chat
            {
                Id = chatId,
                PartitionKey = partitionKey,
                RowKey = EntityKeyPolicy.Row(chatId),
                Title = string.Empty,
                CustomTitle = string.Empty,
                TokensUsed = 0,
                DateChatCreated = nowUtc,
                DateChatUpdated = nowUtc,
                ChatSummary = string.Empty,
                ChatMetadata = string.Empty,
                IsDeleted = false,
                PatientId = patientId,
            };

            var saved = await _chatRepository.SaveAsync(chat, ct);
            return ChatMapper.ToDto(saved, chatId);
        }

        public async Task<ChatDto> GetOrCreateChatAsync(Guid chatId, CancellationToken ct)
        {
            if (chatId == Guid.Empty)
                return await CreateChatAsync(null, ct);

            return await GetChatByIdAsync(chatId, ct);
        }

        public async Task<ChatDto> UpdateChatTitleAsync(Guid chatId, string title, bool isCustomTitle, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(title))
                throw new ArgumentException("Title is required.", nameof(title));

            var partitionKey = BuildTenantUserPartitionKey();
            var chat = await _chatRepository.GetAsync(partitionKey, EntityKeyPolicy.Row(chatId), ct)
                ?? throw new KeyNotFoundException("Chat not found.");

            if (isCustomTitle)
                chat.CustomTitle = title;

            chat.Title = title;
            chat.DateChatUpdated = DateTime.UtcNow;

            var saved = await _chatRepository.SaveAsync(chat, ct);
            return ChatMapper.ToDto(saved, chatId);
        }

        public async Task<ChatDto> UpdateChatSummaryAsync(Guid chatId, string chatSummary, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(chatSummary))
                throw new ArgumentException("Summary is required.", nameof(chatSummary));

            var partitionKey = BuildTenantUserPartitionKey();
            var chat = await _chatRepository.GetAsync(partitionKey, EntityKeyPolicy.Row(chatId), ct)
                ?? throw new KeyNotFoundException("Chat not found.");

            chat.ChatSummary = chatSummary;
            chat.DateChatUpdated = DateTime.UtcNow;

            var saved = await _chatRepository.SaveAsync(chat, ct);
            return ChatMapper.ToDto(saved, chatId);
        }

        public async Task<ChatDto> UpdateAttachedDocumentsAsync(Guid chatId, List<Guid> attachedDocumentsJson, CancellationToken ct)
        {
            var partitionKey = BuildTenantUserPartitionKey();
            var rowKey = EntityKeyPolicy.Row(chatId);

            var chat = await _chatRepository.GetAsync(partitionKey, rowKey, ct)
                ?? throw new KeyNotFoundException("Chat not found.");

            chat.AttachedDocuments = JsonSerializer.Serialize(attachedDocumentsJson);
            chat.DateChatUpdated = DateTime.UtcNow;

            var saved = await _chatRepository.SaveAsync(chat, ct);
            return ChatMapper.ToDto(saved, chatId);
        }

        public async Task<bool> DeleteChatAsync(Guid chatId, CancellationToken ct)
        {
            var chat = await _chatRepository.GetByRowKeyAsync(EntityKeyPolicy.Row(chatId), ct)
                ?? throw new KeyNotFoundException("Chat not found.");

            chat.IsDeleted = true;
            chat.DateChatUpdated = DateTime.UtcNow;
            await _chatRepository.SaveAsync(chat, ct);
            return true;
        }

        public async Task<List<ChatMessageDto>> GetChatMessagesAsync(Guid chatId, int limit = 0, CancellationToken ct = default)
        {
            var partitionKey = BuildTenantUserPartitionKey();
            var messages = await _chatMessageRepository.GetMessagesByChatAsync(partitionKey, chatId, limit, ct);

            if (messages == null || !messages.Any())
                return [];

            return [.. messages.Select(m => ChatMessageMapper.ToDto(m, chatId))];
        }

        public async Task<ChatMessageDto> SaveChatMessageAsync(ChatMessageDto chatMessageDto, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(chatMessageDto.Content))
                throw new ArgumentException("Message is required.", nameof(chatMessageDto.Content));

            var partitionKey = BuildTenantUserPartitionKey();
            var chat = await GetOrCreateChatAsync(chatMessageDto.ChatId, ct);

            chatMessageDto.TokensUsed = TokenEstimation.EstimateTokensForMessage(chatMessageDto.Content);

            var messageId = chatMessageDto.Id == Guid.Empty ? Guid.NewGuid() : chatMessageDto.Id;
            var rowKey = RepositoryKeyHelper.ToOrderedRowKey(chat.Id);

            var chatMessage = ChatMessageMapper.ToEntity(chatMessageDto, partitionKey, rowKey);
            chatMessage.MessageId = messageId;
            chatMessage.Id = messageId;
            chatMessage.ChatTimestamp = DateTime.UtcNow;

            var saved = await _chatMessageRepository.SaveAsync(chatMessage, ct);

            await TokenEstimation.RecalculateTotalTokensAsync(
                chat.Id,
                _userContext,
                _chatRepository,
                _chatMessageRepository,
                ct);

            return ChatMessageMapper.ToDto(saved, chat.Id);
        }

        public async Task<List<ChatDto>> GetChatsForCurrentProviderByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        {
            var partitionKey = BuildTenantUserPartitionKey();
            var chats = await _chatRepository.GetChatsByUserAndPatientIdAsync(partitionKey, patientId, ct);

            return chats
                .Select(c => ChatMapper.ToDto(c, c.Id == Guid.Empty ? RepositoryKeyHelper.FromRowKey(c.RowKey) : c.Id, c.CustomTitle))
                .OrderByDescending(c => c.UpdatedUtc)
                .ToList();
        }

        public async Task<List<ChatDto>> GetChatsForTenantByPatientIdAsync(Guid patientId, CancellationToken ct = default)
        {
            if (!_userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var tenantPrefix = EntityKeyPolicy.TenantPartition(_userContext.TenantId);
            var chats = await _chatRepository.GetChatsByTenantAndPatientIdAsync(tenantPrefix, patientId, ct);

            return chats
                .Select(c => ChatMapper.ToDto(c, c.Id == Guid.Empty ? RepositoryKeyHelper.FromRowKey(c.RowKey) : c.Id, c.CustomTitle))
                .OrderByDescending(c => c.UpdatedUtc)
                .ToList();
        }
    }
}

