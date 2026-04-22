using MedInsights.Lib.Models;

namespace MedInsights.Services.BackgroundServices.Interfaces;

public interface IEncounterTranscriptionQueue
{
    ValueTask QueueJobAsync(EncounterTranscriptionJob job);
    IAsyncEnumerable<EncounterTranscriptionJob> DequeueAsync(CancellationToken cancellationToken);
}

