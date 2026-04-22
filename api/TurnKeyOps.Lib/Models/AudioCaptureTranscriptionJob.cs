
namespace MedInsights.Lib.Models;

public class AudioCaptureTranscriptionJob
{
    // Identity of the capture in your system
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;

    // Where the audio lives
    public string AudioBlobContainer { get; set; } = default!;
    public string AudioBlobName { get; set; } = default!;

    // Where the worker should POST the result (relative to API base URL)
    public string CallbackPath { get; set; } = default!;

    // Optional per-job token for auth
    public string? JobToken { get; set; }

    // Optional hint (dictation vs encounter vs whatever)
    public string? Scenario { get; set; }
}




public class AudioTranscriptionResultDto
{
    public Guid CorrelationId { get; set; }
    public string Status { get; set; } = default!;
    public string TranscribedText { get; set; } = default!;

    // public int? SpeechTokenCount { get; set; }
}
