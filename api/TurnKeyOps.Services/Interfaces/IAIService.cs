using System.Runtime.CompilerServices;
using System.Text.Json;
using MedInsights.Lib.Dtos;

namespace MedInsights.Services.Interfaces
{
    public interface IAIService<TChatMessage>
    {
        /// <summary>
        /// Build the provider-specific chat message list to send to the model
        /// from system/doc/summary prompts plus stored + incoming messages.
        /// </summary>
        List<TChatMessage> BuildChatMessages(
            string systemPrompt,
            string documentsPrompt,
            string summary,
            List<ChatMessageDto> chatMessages,
            JsonElement payload);

        /// <summary>
        /// Stream completion tokens from the model as plain text chunks.
        /// </summary>
        IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<TChatMessage> messages,
            CancellationToken ct = default);

        ///<summary>
        /// Stream completion tokens with tool from the model as plain text
        /// </summmary>
        IAsyncEnumerable<AiDelta> StreamChatWithToolsAsync(
            IEnumerable<TChatMessage> messages,
            JsonElement originalPayload,
            CancellationToken ct = default);

        /// <summary>
        /// Convenience helper for non-streaming use cases
        /// (summaries, titles, internal utilities).
        /// </summary>
        Task<string> GetChatCompletionAsync(
            string systemPrompt,
            IEnumerable<string> userMessages,
            int maxOutputTokens,
            double temperature = 0.1,
            CancellationToken ct = default);

        /// <summary>
        /// Set the model to use for subsequent calls (instance-level).
        /// </summary>
        void SetServiceModel(string model);
    }
}
