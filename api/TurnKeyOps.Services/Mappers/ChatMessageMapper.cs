using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.Mappers
{
    public static class ChatMessageMapper
    {
        public static ChatMessageDto ToDto(ChatMessage entity, Guid chatId)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            return new ChatMessageDto
            {
                Id = entity.MessageId,
                ChatId = chatId,
                Role = entity.Role,
                Content = entity.Content,
                Timestamp = DateTime.SpecifyKind(entity.ChatTimestamp, DateTimeKind.Utc),
                TokensUsed = entity.TokensUsed
            };
        }

        public static ChatMessage ToEntity(ChatMessageDto dto, string partitionKey, string rowKey)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            return new ChatMessage
            {
                Id = dto.Id,
                MessageId = dto.Id,
                PartitionKey = partitionKey,
                RowKey = rowKey,
                Role = dto.Role,
                Content = dto.Content,
                ChatTimestamp = DateTime.SpecifyKind(dto.Timestamp, DateTimeKind.Utc),
                TokensUsed = dto.TokensUsed,
                IsDeleted = false
            };
        }
    }
}
