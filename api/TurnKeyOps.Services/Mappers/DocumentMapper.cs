using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class DocumentMapper
    {
        public static DocumentDto MapToResultDto(Document entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new DocumentDto
            {
                Id = id,
                UserId = entity.UserId,
                FileName = entity.FileName,
                BlobUrl = entity.BlobUrl ?? string.Empty,
                Size = entity.Size,
                ContentType = entity.ContentType,
                UploadedAt = entity.UploadedAt,
                Category = entity.Category,
                PatientId = entity.PatientId,
                ChatId = entity.ChatId,
                Message = "File uploaded successfully."
            };
        }

        public static Document MapToEntity(DocumentDto dto)
        {
            return new Document
            {
                Id = dto.Id,
                UserId = dto.UserId,
                FileName = dto.FileName,
                BlobUrl = dto.BlobUrl,
                Size = dto.Size,
                ContentType = dto.ContentType,
                UploadedAt = dto.UploadedAt,
                Category = dto.Category,
                PatientId = dto.PatientId,
                ChatId = dto.ChatId,
                File = null,
                IsDeleted = false
            };
        }
    }
}
