

namespace MedInsights.Lib.Dtos
{
    public class AudioCaptureTranscriptDto
    {        
        public Guid Id { get; set; } = default!;

        public string Status { get; set; } = ""; // e.g., Pending, InProgress, Completed
        public string? TranscribedText { get; set; } = null;

        // 🔥 NEW FIELD: Processing stage for visibility into pipeline progress
        public string ProcessingStage { get; set; } = ""; 
            // Examples: Recording, Uploaded, Queued, DownloadingAudio, ConvertingAudio, Transcribing, Completed, Failed

        public string? JobToken { get; set; }

        public string? JobKey { get; set; }

    }
}
