using Azure;
using Azure.Data.Tables;
using IBeam.Repositories.Abstractions;

namespace MedInsights.Lib.Entities
{
    public class Dictation : IEntity, ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;

        public Guid Id { get; set; }
        public bool IsDeleted { get; set; }

        public string PatientId { get; set; } = "";
        public string AudioFileUrl { get; set; } = "";
        public string Status { get; set; } = "";
        public string? TranscribedText { get; set; } = null;

        public string ProcessingStage { get; set; } = "";
        public int RetryCount { get; set; } = 0;
        public int? SpeechTokenCount { get; set; }
        public decimal? EstimatedCostUsd { get; set; }

        public DateTimeOffset DateCreated { get; set; }
        public DateTimeOffset DateUpdated { get; set; }

        public ETag ETag { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
    }
}
