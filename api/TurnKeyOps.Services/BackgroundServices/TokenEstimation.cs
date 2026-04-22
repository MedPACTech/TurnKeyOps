using MedInsights.Repositories.Interfaces;
using MedInsights.Lib.Utils;
using MedInsights.Lib.Entities;

namespace MedInsights.Services.BackgroundServices
{
    /// <summary>
    /// Provides token estimation utilities and persistent recalculation methods.
    /// </summary>
    public static class TokenEstimation
    {
        /// <summary>
        /// Estimate tokens for a single message string.
        /// </summary>
        public static int EstimateTokensForMessage(string text)
        {
            var len = string.IsNullOrEmpty(text) ? 0 : text.Length;
            return Math.Max(1, len / 3); // ~3 characters per token heuristic
        }

        /// <summary>
        /// Estimate total tokens for a set of messages in-memory (stateless).
        /// </summary>
        public static int EstimateTokensForConversation(IEnumerable<string> messageContents)
        {
            int chars = messageContents?.Sum(c => c?.Length ?? 0) ?? 0;
            return Math.Max(1, chars / 3);
        }

        /// <summary>
        /// Recalculate and persist total token usage for a conversation.
        /// </summary>
        /// <param name="chatId">Chat GUID</param>
        /// <param name="userContext">Provides tenant/user identifiers</param>
        /// <param name="chatRepo">Chat repository</param>
        /// <param name="chatMessageRepo">Message repository</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Total estimated tokens across the conversation</returns>
        public static async Task<int> RecalculateTotalTokensAsync(
            Guid chatId,
            IUserContext userContext,
            IChatRepository chatRepo,
            IChatMessageRepository chatMessageRepo,
            CancellationToken ct = default)
        {
            if (!userContext.IsAuthenticated)
                throw new UnauthorizedAccessException();

            var partitionKey = EntityKeyPolicy.TenantUserPartition(
                userContext.TenantId,
                userContext.UserId
            );

            var allMessages = await chatMessageRepo.GetMessagesByChatAsync(partitionKey, chatId, 0);
            if (allMessages == null || !allMessages.Any())
                return 0;

            // Total existing or estimated tokens
            int totalTokens = allMessages.Sum(m =>
                m.TokensUsed > 0 ? m.TokensUsed : EstimateTokensForMessage(m.Content)
            );

            // Update chat record
            var chat = await chatRepo.GetAsync(partitionKey, EntityKeyPolicy.Row(chatId), ct);
            if (chat != null)
            {
                chat.TokensUsed = totalTokens;
                chat.DateChatUpdated = DateTime.UtcNow;
                await chatRepo.SaveAsync(chat, ct);
            }

            return totalTokens;
        }
    }
}


