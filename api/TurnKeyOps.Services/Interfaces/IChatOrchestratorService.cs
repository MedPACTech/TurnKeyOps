// MedInsights.Services/IChatOrchestratorService.cs
using System.Text.Json;
using MedInsights.Lib.Dtos;
using Microsoft.AspNetCore.Http;

namespace MedInsights.Services.Interfaces
{
    public interface IChatOrchestratorService
    {
        /// <summary>
        /// Main entry point for handling a chat request:
        /// - Validates payload
        /// - Loads chat + history
        /// - Saves latest user message
        /// - Builds prompts (system, documents, summary)
        /// - Handles DEBUG short-circuit (in dev)
        /// - Streams OpenAI response to the client via SSE
        /// - Kicks off post-completion processing
        /// </summary>
        Task StreamChatResponseAsync(
            JsonElement payload,
            HttpResponse response,
            CancellationToken ct = default);

        /// <summary>
        /// Transient (non-persistent) streaming chat:
        /// - Uses short-lived in-memory session history for context
        /// - Never writes chats/messages/summaries/titles to storage
        /// </summary>
        Task StreamTransientChatResponseAsync(
            JsonElement payload,
            HttpResponse response,
            CancellationToken ct = default);

        /// <summary>
        /// Handles post-completion work:
        /// - Saves assistant message
        /// - Updates summary and title
        /// </summary>
        Task HandlePostCompletionAsync(
            Guid chatId,
            List<ChatMessageDto> chatMessages,
            string existingSummary,
            string newTitle,
            string assistantText,
            CancellationToken ct = default);

        /// <summary>
        /// Saves an entire chat transcript to storage and returns the updated summary.
        /// </summary>
        Task<string> SaveTranscriptAsync(
            Guid chatId,
            List<ChatMessageDto> chatMessages,
            CancellationToken ct = default);
    }
}
