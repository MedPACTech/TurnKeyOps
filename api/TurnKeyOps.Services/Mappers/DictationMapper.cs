using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class DictationMapper
    {
        public static AudioCaptureDto ToDto(Dictation entity)
        {
            return new AudioCaptureDto
            {
                Id                = RepositoryKeyHelper.FromRowKey(entity.RowKey),
                PatientId         = entity.PatientId ?? string.Empty,
                Status            = entity.Status,
                TranscribedText   = entity.TranscribedText,

                // New fields
                ProcessingStage   = entity.ProcessingStage ?? string.Empty,
                RetryCount        = entity.RetryCount,
                SpeechTokenCount  = entity.SpeechTokenCount,
                EstimatedCostUsd  = entity.EstimatedCostUsd,
                AudioFileUrl      = entity.AudioFileUrl ?? string.Empty,

                CreatedAt         = entity.DateCreated,
                UpdatedAt         = entity.DateUpdated
            };
        }

        public static Dictation ToEntity(AudioCaptureDto dto, string partitionKey)
        {
            return new Dictation
            {
                PartitionKey = partitionKey,
                RowKey = EntityKeyPolicy.Row(dto.Id),

                PatientId = dto.PatientId,
                Status = dto.Status,
                TranscribedText = dto.TranscribedText,

                // New fields mapped
                ProcessingStage = dto.ProcessingStage,
                RetryCount = dto.RetryCount,
                SpeechTokenCount = dto.SpeechTokenCount,
                EstimatedCostUsd = dto.EstimatedCostUsd,
                AudioFileUrl = dto.AudioFileUrl,

                // Use DateTimeOffset consistently
                DateCreated = dto.CreatedAt == default
                    ? DateTimeOffset.UtcNow
                    : dto.CreatedAt,

                DateUpdated = DateTimeOffset.UtcNow,

                // Keep IsDeleted default unless provided later
                IsDeleted = false
            };
        }
    }
}

