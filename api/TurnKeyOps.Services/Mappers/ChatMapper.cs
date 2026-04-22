using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using System.Text.Json;

namespace MedInsights.Services.Mappers
{
    public static class ChatMapper
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static List<Guid> DeserializeAttachedDocuments(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new List<Guid>();

            try
            {
                return JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions) ?? new List<Guid>();
            }
            catch
            {
                return new List<Guid>();
            }
        }

        private static string SerializeAttachedDocuments(ICollection<Guid>? docs)
        {
            var safeList = docs ?? Array.Empty<Guid>();
            return JsonSerializer.Serialize(safeList, JsonOptions);
        }

        public static ChatDto ToDto(Chat entity, Guid id, string? customTitle = null)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            return new ChatDto
            {
                Id = id,
                Summary = entity.ChatSummary,
                Title = string.IsNullOrWhiteSpace(customTitle) ? entity.Title : customTitle,
                AttachedDocuments = DeserializeAttachedDocuments(entity.AttachedDocuments),
                UpdatedUtc = DateTime.SpecifyKind(entity.DateChatUpdated, DateTimeKind.Utc),
                TokensUsed = entity.TokensUsed,
                PatientId = entity.PatientId
            };
        }

        public static Chat ToEntity(ChatDto dto, string partitionKey, string rowKey)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            return new Chat
            {
                Id = dto.Id,
                PartitionKey = partitionKey,
                RowKey = rowKey,
                Title = dto.Title,
                CustomTitle = dto.Title,
                ChatSummary = dto.Summary,
                DateChatCreated = DateTime.SpecifyKind(dto.UpdatedUtc, DateTimeKind.Utc),
                DateChatUpdated = DateTime.SpecifyKind(dto.UpdatedUtc, DateTimeKind.Utc),
                TokensUsed = dto.TokensUsed,
                IsDeleted = false,
                AttachedDocuments = SerializeAttachedDocuments(dto.AttachedDocuments),
                PatientId = dto.PatientId
            };
        }
    }
}
