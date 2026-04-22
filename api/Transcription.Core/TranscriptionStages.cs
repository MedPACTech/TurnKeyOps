namespace Transcription.Core;

/// <summary>
/// Granular pipeline stages for UI + operational insight.
/// Stored on Dictation / PatientEncounter entities.
/// </summary>
public static class TranscriptionStages
{
    public const string Recording    = "Recording";
    public const string Uploaded     = "Uploaded";
    public const string Queued       = "Queued";
    public const string Transcribing = "Transcribing";
    public const string Summarizing  = "Summarizing";
    public const string Completed    = "Completed";
    public const string Failed       = "Failed";
}

