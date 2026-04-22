namespace Transcription.Core;

/// <summary>
/// Queue message contract shared between API and Azure Functions.
/// API enqueues this; Functions dequeues and processes it.
/// </summary>
public record TranscriptionJob(
    string PartitionKey,
    string RowKey,
    TranscriptionUseCase UseCase
);
