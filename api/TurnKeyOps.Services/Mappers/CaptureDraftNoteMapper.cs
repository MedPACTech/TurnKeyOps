using MedInsights.Lib.Dtos;
using MedInsights.Lib.Entities;
using MedInsights.Lib.Utils;

namespace MedInsights.Services.Mappers
{
    public static class CaptureDraftNoteMapper
    {
        public static CaptureDraftNoteDto ToDto(CaptureDraftNote e)
        {
            var id = e.Id;
            if (id == Guid.Empty && !string.IsNullOrWhiteSpace(e.RowKey) && Guid.TryParse(e.RowKey, out var parsedId))
                id = parsedId;

            return new CaptureDraftNoteDto
            {
                Id = id,
                PatientId = string.IsNullOrWhiteSpace(e.PatientId) ? null : RepositoryKeyHelper.FromRowKey(e.PatientId),
                ProviderId = string.IsNullOrWhiteSpace(e.ProviderId) ? Guid.Empty : RepositoryKeyHelper.FromRowKey(e.ProviderId),
                CaptureSourceType = e.CaptureSourceType ?? string.Empty,
                CaptureSourceId = string.IsNullOrWhiteSpace(e.CaptureSourceId) ? null : RepositoryKeyHelper.FromRowKey(e.CaptureSourceId),
                CaptureSourceText = e.CaptureSourceText ?? string.Empty,
                CaptureSourceAddendum = e.CaptureSourceAddendum ?? string.Empty,
                CaptureStatus = e.CaptureStatus ?? string.Empty,
                NoteType = e.NoteType ?? string.Empty,
                NoteTitle = e.NoteTitle ?? string.Empty,
                NoteBody = e.NoteBody ?? string.Empty,
                BillingBody = e.BillingBody ?? string.Empty,
                CommunicationBody = e.CommunicationBody ?? string.Empty,
                DateCreated = e.DateCreated,
                CreatedBy = e.CreatedBy,
                Tags = e.Tags ?? string.Empty
            };
        }

        public static CaptureDraftNote ToEntity(CaptureDraftNoteDto dto, string partitionKey)
        {
            var id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id;

            return new CaptureDraftNote
            {
                Id = id,
                PartitionKey = partitionKey,
                RowKey = EntityKeyPolicy.Row(id),
                PatientId = dto.PatientId.HasValue ? EntityKeyPolicy.Row(dto.PatientId.Value) : string.Empty,
                ProviderId = dto.ProviderId.HasValue && dto.ProviderId.Value != Guid.Empty
                    ? EntityKeyPolicy.Row(dto.ProviderId.Value)
                    : string.Empty,
                CaptureSourceType = dto.CaptureSourceType ?? string.Empty,
                CaptureSourceId = dto.CaptureSourceId.HasValue ? EntityKeyPolicy.Row(dto.CaptureSourceId.Value) : null,
                CaptureSourceText = dto.CaptureSourceText ?? string.Empty,
                CaptureSourceAddendum = dto.CaptureSourceAddendum ?? string.Empty,
                CaptureStatus = dto.CaptureStatus ?? string.Empty,
                NoteType = dto.NoteType ?? string.Empty,
                NoteTitle = dto.NoteTitle ?? string.Empty,
                NoteBody = dto.NoteBody ?? string.Empty,
                BillingBody = dto.BillingBody ?? string.Empty,
                CommunicationBody = dto.CommunicationBody ?? string.Empty,
                DateCreated = dto.DateCreated == default ? DateTime.UtcNow : dto.DateCreated,
                DateUpdated = DateTime.UtcNow,
                CreatedBy = dto.CreatedBy,
                Tags = dto.Tags ?? string.Empty,
                IsDeleted = false
            };
        }
    }
}

