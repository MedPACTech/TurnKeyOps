using MedInsights.Lib.Models;

namespace MedInsights.Services.BackgroundServices.Interfaces;

public interface IAudioCaptureTranscriptionQueue
{
    Task QueueJobAsync(AudioCaptureTranscriptionJob job, CancellationToken ct = default);
}
