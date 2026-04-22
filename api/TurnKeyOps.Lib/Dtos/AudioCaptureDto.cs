using System;
using Microsoft.AspNetCore.Http;

namespace MedInsights.Lib.Dtos
{
    public class AudioCaptureDto
    {
         public IFormFile File { get; set; } = default!;
         
        public Guid Id { get; set; } = default!;

        public string PatientId { get; set; } = "";
        public string Status { get; set; } = ""; // e.g., Pending, InProgress, Completed
        public string? TranscribedText { get; set; } = null;

        // 🔥 NEW FIELD: Processing stage for visibility into pipeline progress
        public string ProcessingStage { get; set; } = ""; 
            // Examples: Recording, Uploaded, Queued, DownloadingAudio, ConvertingAudio, Transcribing, Completed, Failed

        // 🔥 NEW FIELD: Retry count for worker attempts
        public int RetryCount { get; set; }

        // 🔥 NEW FIELD: Speech token usage (Azure Speech to Text)
        public int? SpeechTokenCount { get; set; }

        // 🔥 NEW FIELD: Estimated cost for dictation transcription
        public decimal? EstimatedCostUsd { get; set; }

        // 🔥 NEW FIELD: Blob location for the uploaded audio
        public string AudioFileUrl { get; set; } = "";

        public string? JobToken { get; set; }

        public string? JobKey { get; set; }

        // Timestamps converted to DateTimeOffset (already correct)
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
