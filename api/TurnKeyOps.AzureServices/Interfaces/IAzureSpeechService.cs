using MedInsights.AzureServices.Lib;

namespace MedInsights.AzureServices.Interfaces
{
    public interface IAzureSpeechService
    {
        Task<string> TranscribeDictationAsync(Stream audioStream, string locale = "en-US", CancellationToken ct = default);
        Task<string> TranscribeConversationAsync(Stream audioStream, string locale = "en-US", CancellationToken ct = default);

        /// <summary>
        /// The most recent audit records produced during the last transcription call.
        /// Persist this externally if needed for legal/clinical auditing.
        /// </summary>
        IReadOnlyList<RedactionHit> LastAudit { get; }
    }
}