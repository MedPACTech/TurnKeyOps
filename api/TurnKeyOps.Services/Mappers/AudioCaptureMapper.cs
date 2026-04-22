using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class AudioCaptureMapper
    {
        public static AudioCaptureDto ToDto(AudioCapture entity)
        {
            var id = entity.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(entity.RowKey) && Guid.TryParse(entity.RowKey, out var parsedId))
                id = parsedId;

            return new AudioCaptureDto
            {
                Id = id,
                PatientId = entity.PatientId ?? string.Empty,
                Status = entity.Status,
                TranscribedText = entity.TranscribedText,
                ProcessingStage = entity.ProcessingStage ?? string.Empty,
                RetryCount = entity.RetryCount,
                SpeechTokenCount = entity.SpeechTokenCount,
                EstimatedCostUsd = entity.EstimatedCostUsd,
                AudioFileUrl = entity.AudioFileUrl ?? string.Empty,
                CreatedAt = entity.DateCreated,
                UpdatedAt = entity.DateUpdated,
                JobToken = entity.JobToken,
                JobKey = entity.PartitionKey
            };
        }

        public static AudioCapture ToEntity(AudioCaptureDto dto, string partitionKey)
        {
            return new AudioCapture
            {
                Id = dto.Id,
                PartitionKey = partitionKey,
                RowKey = EntityKeyPolicy.Row(dto.Id),
                PatientId = dto.PatientId,
                Status = dto.Status,
                TranscribedText = dto.TranscribedText,
                ProcessingStage = dto.ProcessingStage,
                RetryCount = dto.RetryCount,
                SpeechTokenCount = dto.SpeechTokenCount,
                EstimatedCostUsd = dto.EstimatedCostUsd,
                AudioFileUrl = dto.AudioFileUrl,
                DateCreated = dto.CreatedAt == default ? DateTimeOffset.UtcNow : dto.CreatedAt,
                DateUpdated = DateTimeOffset.UtcNow,
                JobToken = dto.JobToken,
                IsDeleted = false
            };
        }
    }
}

