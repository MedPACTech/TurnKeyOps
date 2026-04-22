namespace Transcription.Core;

/// <summary>
/// Which transcription scenario this audio belongs to.
/// Used primarily for routing to the correct STT model.
/// </summary>
public enum TranscriptionUseCase
{
    DictationSingleSpeaker = 0,
    EncounterMultiSpeaker  = 1
}
