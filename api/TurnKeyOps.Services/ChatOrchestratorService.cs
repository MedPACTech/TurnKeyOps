using MedInsights.Lib;
using MedInsights.Lib.Configurations;
using MedInsights.Lib.Dtos;
using MedInsights.Models;
using MedInsights.Services.BackgroundServices;
using MedInsights.Services.Interfaces;
using MedInsights.Lib.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.Text;
using System.Text.Json;

namespace MedInsights.Services
{
    public sealed class ChatOrchestratorService : IChatOrchestratorService
    {
        private readonly IChatService _chatService;
        private readonly OpenAISettings _openAPISettings;
        private readonly IChatSummaryService _chatSummaryService;
        private readonly IChatTitleService _chatTitleService;
        private readonly IDocumentService _documentService;
        private readonly IAIService<ChatMessage> _aIService;
        private readonly IHostEnvironment _env;
        private readonly IMemoryCache _memoryCache;
        private readonly IUserContext _userContext;

        private const int TransientMaxTurns = 20;
        private static readonly TimeSpan TransientSessionTtl = TimeSpan.FromMinutes(45);

        public ChatOrchestratorService(
            IOptions<OpenAISettings> openAISettings,
            IChatService chatService,
            IChatSummaryService chatSummaryService,
            IChatTitleService chatTitleService,
            IDocumentService documentService,
            IAIService<ChatMessage> aiService,
            IHostEnvironment env,
            IMemoryCache memoryCache,
            IUserContext userContext)
        {
            _chatService = chatService;
            _openAPISettings = openAISettings.Value;
            _chatSummaryService = chatSummaryService;
            _chatTitleService = chatTitleService;
            _documentService = documentService;
            _aIService = aiService;
            _env = env;
            _memoryCache = memoryCache;
            _userContext = userContext;
        }

        #region Public Entry Point
                //Chat Stream Helpers
        public async Task<string> SaveTranscriptAsync(Guid chatId, List<ChatMessageDto> chatMessages, CancellationToken cancellationToken)
        {
            var existingChat = await _chatService.GetChatByIdAsync(chatId, cancellationToken);
            if (existingChat == null)
            {
                throw new InvalidOperationException($"Chat with ID {chatId} does not exist.");
            }

            foreach (var msg in chatMessages)
            {
                msg.ChatId = chatId; //ensure correct chat association
                await _chatService.SaveChatMessageAsync(msg, cancellationToken);
            }

            var summaryRequest = new SummaryRequest(
                SessionId: chatId,
                ChatMessages: chatMessages,
                ExistingSummary: existingChat.Summary,
                Style: SummarizeStyle.Json,
                TargetTokens: 400,
                RedactSensitive: false
            );

            var summaryJson = await _chatSummaryService.SummarizeAsync(summaryRequest, cancellationToken);
            var summaryText = summaryJson.RootElement
                .GetProperty("summary")
                .GetProperty("text")
                .GetString() ?? "";

            var title = await _chatTitleService.GenerateTitleAsync("New Chat", string.Empty, string.Join("\n", chatMessages.Where(m => m.Role == "user").Select(m => m.Content)), summaryText);

            await _chatService.UpdateChatTitleAsync(chatId, title, false, cancellationToken);
            await _chatService.UpdateChatSummaryAsync(chatId, summaryText, cancellationToken);

            return title;
        }

        public async Task StreamChatResponseAsync(JsonElement payload, HttpResponse response, CancellationToken ct = default)
        {
            // 0️⃣ Basic request validation
            if (!payload.TryGetProperty("messages", out var messagesProp))
            {
                await WriteBadRequestAsync(response, "Missing 'messages' property in request payload.");
                return;
            }

            if (!payload.TryGetProperty("chatId", out var chatIdProp) ||
                chatIdProp.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(chatIdProp.GetString()))
            {
                await WriteBadRequestAsync(response, "Missing or invalid 'chatId' in payload.");
                return;
            }

            // 1️⃣ Ensure chat exists and load context
            var chatId = Guid.Parse(chatIdProp.GetString()!);
            var chat = await _chatService.GetOrCreateChatAsync(chatId, ct);
            var existingTitle = chat.Title;
            var existingSummary = chat.Summary ?? string.Empty;

            var chatMessages = await _chatService.GetChatMessagesAsync(chat.Id, 0, ct);

            // 2️⃣ Save latest user message (if new) and capture its text
            var lastUserTextFromPayload =
                await SaveLatestUserMessageAsync(chat, messagesProp, chatMessages, ct);

            var lastUserMessage =
                lastUserTextFromPayload ??
                chatMessages.LastOrDefault(m => m.Role == "user")?.Content ??
                string.Empty;

            // 3️⃣ Build prompts (system, documents, summary)
            var systemPrompt = await GetSystemPromptForChatAsync(chat.Id, ct);
            var documentsPrompt = await PrepareDocumentsPromptForChat(chat.AttachedDocuments, ct);

            // 4️⃣ appended System Prompt from payload
            systemPrompt =
                await AppendSystemPrompt(chat, messagesProp, systemPrompt, ct);

            // 5 DEBUG short-circuit in development
            if (_env.IsDevelopment() &&
                !string.IsNullOrWhiteSpace(lastUserMessage) &&
                lastUserMessage.StartsWith("DEBUG", StringComparison.OrdinalIgnoreCase))
            {
                var debugText = HandleDebugCommand(
                    lastUserMessage.Trim(),
                    systemPrompt,
                    documentsPrompt,
                    existingSummary);

                await StreamDebugTextAsSseAsync(response, debugText, chat.Id, existingTitle, ct);
                return;
            }

            // 5️⃣ Build OpenAI ChatMessages (SDK)
            var aiMessages = _aIService.BuildChatMessages(
                systemPrompt,
                documentsPrompt,
                existingSummary,
                chatMessages,
                payload);    

            // Update Title with response
            var newTitle = await _chatTitleService.GenerateTitleAsync(
                existingTitle,
                lastUserMessage,
                string.Empty,
                existingSummary
                );    

            // 6️⃣ Stream assistant response via OpenAI SDK → SSE
            var streamResult = await StreamAssistantResponseAsync(
                response,
                aiMessages,
                payload,
                chat.Id,
                newTitle,
                true,
                ct);

            // 7️⃣ Persist assistant + update summary/title (if we actually got text)
            if (streamResult.SawText && !string.IsNullOrWhiteSpace(streamResult.AssistantText))
            {
                _ = Task.Run(async () =>
                {
                    using var postCts = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                    try
                    {
                        await HandlePostCompletionAsync(
                            chat.Id,
                            chatMessages,
                            existingSummary,
                            newTitle,
                            streamResult.AssistantText,
                            postCts.Token);
                    }
                    catch (OperationCanceledException) when (postCts.IsCancellationRequested)
                    {
                        // expected timeout/shutdown
                        throw;
                    }
                }, CancellationToken.None);
            }

            // IMPORTANT: Do not write anything else to Response here.
            return;
        }

        private async Task<string> AppendSystemPrompt(ChatDto chat, JsonElement messagesProp, string systemPrompt, CancellationToken ct)
        {
            // Check for additional system prompt in payload
            foreach (var msgEl in messagesProp.EnumerateArray())
            {
                if (msgEl.ValueKind == JsonValueKind.Object &&
                    msgEl.TryGetProperty("role", out var roleProp) &&
                    string.Equals(roleProp.GetString(), "system", StringComparison.OrdinalIgnoreCase) &&
                    msgEl.TryGetProperty("content", out var contentProp) &&
                    contentProp.ValueKind == JsonValueKind.String)
                {
                    var additionalPrompt = contentProp.GetString();
                    if (!string.IsNullOrWhiteSpace(additionalPrompt))
                    {
                        systemPrompt +=
                            "\n[ADDITIONAL_SYSTEM_PROMPT_START]\n" +
                            additionalPrompt +
                            "\n[ADDITIONAL_SYSTEM_PROMPT_END]";
                    }
                }
            }

            return systemPrompt;
        }

        #endregion

        #region Public Post-Completion Helper

        public async Task HandlePostCompletionAsync(
            Guid chatId,
            List<ChatMessageDto> chatMessages,
            string existingSummary,
            string newTitle,
            string assistantText,
            CancellationToken ct)
        {
            var newAssistantMsg = new ChatMessageDto
            {
                Id = Guid.NewGuid(),
                ChatId = chatId,
                Role = "assistant",
                Content = assistantText,
                Timestamp = DateTime.UtcNow,
                TokensUsed = TokenEstimation.EstimateTokensForMessage(assistantText)
            };

            await _chatService.SaveChatMessageAsync(newAssistantMsg, ct);

            var summaryRequest = new SummaryRequest(
                SessionId: chatId,
                ChatMessages: chatMessages.Append(newAssistantMsg).ToList(),
                ExistingSummary: existingSummary,
                Style: SummarizeStyle.Json,
                TargetTokens: 400,
                RedactSensitive: false
            );

            var summaryJson = await _chatSummaryService.SummarizeAsync(summaryRequest, ct);
            var summaryText = summaryJson.RootElement
                .GetProperty("summary")
                .GetProperty("text")
                .GetString() ?? "";

            if (!string.IsNullOrWhiteSpace(newTitle))
            {
                await _chatService.UpdateChatTitleAsync(chatId, newTitle.Trim(), false, ct);
            }

            await _chatService.UpdateChatSummaryAsync(chatId, summaryText, ct);

            return;
        }

        public async Task StreamTransientChatResponseAsync(JsonElement payload, HttpResponse response, CancellationToken ct = default)
        {
            if (!payload.TryGetProperty("messages", out var messagesProp))
            {
                await WriteBadRequestAsync(response, "Missing 'messages' property in request payload.");
                return;
            }

            if (!TryGetTransientSessionId(payload, out var sessionId))
            {
                await WriteBadRequestAsync(response, "Missing or invalid 'sessionId' for transient chat.");
                return;
            }

            var cacheKey = BuildTransientCacheKey(sessionId);
            if (payload.TryGetProperty("resetSession", out var resetProp) &&
                resetProp.ValueKind == JsonValueKind.True)
            {
                _memoryCache.Remove(cacheKey);
            }

            var state = _memoryCache.Get<TransientChatState>(cacheKey) ?? new TransientChatState();
            var priorTurns = state.Messages.ToList();
            var latestUserText = ExtractLatestUserText(messagesProp);

            var systemPrompt = BuildTransientSystemPrompt(payload);
            var aiMessages = _aIService.BuildChatMessages(
                systemPrompt,
                documentsPrompt: string.Empty,
                summary: string.Empty,
                chatMessages: priorTurns,
                payload: payload);

            var streamResult = await StreamAssistantResponseAsync(
                response,
                aiMessages,
                payload,
                chatId: sessionId,
                title: "Transient Chat",
                withTools: true,
                ct: ct);

            if (!string.IsNullOrWhiteSpace(latestUserText) &&
                IsDifferentFromLastPersistedUser(state.Messages, latestUserText))
            {
                state.Messages.Add(new ChatMessageDto
                {
                    Id = Guid.NewGuid(),
                    ChatId = sessionId,
                    Role = "user",
                    Content = latestUserText,
                    Timestamp = DateTime.UtcNow,
                    TokensUsed = 0
                });
            }

            if (streamResult.SawText && !string.IsNullOrWhiteSpace(streamResult.AssistantText))
            {
                state.Messages.Add(new ChatMessageDto
                {
                    Id = Guid.NewGuid(),
                    ChatId = sessionId,
                    Role = "assistant",
                    Content = streamResult.AssistantText,
                    Timestamp = DateTime.UtcNow,
                    TokensUsed = TokenEstimation.EstimateTokensForMessage(streamResult.AssistantText)
                });
            }

            TrimTransientTurns(state.Messages);
            _memoryCache.Set(cacheKey, state, new MemoryCacheEntryOptions
            {
                SlidingExpiration = TransientSessionTtl,
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(4)
            });
        }

        #endregion

        #region Debug Helpers

        private async Task StreamDebugTextAsSseAsync(
            HttpResponse response,
            string text,
            Guid chatId,
            string title,
            CancellationToken ct)
        {
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = "text/event-stream";
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["X-Accel-Buffering"] = "no";

            var metaJson = JsonSerializer.Serialize(new { type = "meta", meta = new { chatId, title } });
            await response.WriteAsync($"data: {metaJson}\n\n");
            await response.Body.FlushAsync();

            var dataJson = JsonSerializer.Serialize(new { type = "data", delta = new { content = text } });
            await response.WriteAsync($"data: {dataJson}\n\n");

            var doneJson = JsonSerializer.Serialize(new { type = "done" });
            await response.WriteAsync($"data: {doneJson}\n\n");
            await response.Body.FlushAsync();
        }

        private string HandleDebugCommand(
            string command,
            string systemPrompt,
            string documentsPrompt,
            string summaryPrompt)
        {
            switch (command.Trim())
            {
                case "DEBUG:SHOW_SYSTEM_PROMPT":
                    return $"[SYSTEM PROMPT]\n{systemPrompt}";

                case "DEBUG:SHOW_DOCUMENT_CONTEXT":
                    return $"[DOCUMENTS PROMPT]\n{documentsPrompt}";

                case "DEBUG:SHOW_SUMMARY_PROMPT":
                    return $"[SUMMARY PROMPT]\nPrevious summary context:\n{summaryPrompt}";

                default:
                    return "Unknown DEBUG command. Try:\n" +
                           "- DEBUG:SHOW_SYSTEM_PROMPT\n" +
                           "- DEBUG:SHOW_DOCUMENT_CONTEXT\n" +
                           "- DEBUG:SHOW_SUMMARY_PROMPT";
            }
        }

        #endregion

        #region Streaming to OpenAI (SDK) → SSE

        private async Task<StreamResult> StreamAssistantResponseAsync(
            HttpResponse response,
            IEnumerable<ChatMessage> messages,
            JsonElement originalPayload,
            Guid chatId,
            string title,
            bool withTools = false,
            CancellationToken ct = default)
        {
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = "text/event-stream";
            response.Headers["Cache-Control"] = "no-cache";
            response.Headers["X-Accel-Buffering"] = "no";

            // meta event
            var metaJson = JsonSerializer.Serialize(new { type = "meta", meta = new { chatId, title } });
            await response.WriteAsync($"data: {metaJson}\n\n");
            await response.Body.FlushAsync();

            var assistantBuffer = new StringBuilder();
            bool sawText = false;
            bool sawTools = false; // reserved for future tool support

            if(!withTools)
            {
                    await foreach (var chunk in _aIService.StreamChatAsync(messages, ct))
                    {
                        if (string.IsNullOrEmpty(chunk))
                            continue;

                        sawText = true;
                        assistantBuffer.Append(chunk);

                        var outJson = JsonSerializer.Serialize(new
                        {
                            type = "data",
                            delta = new { content = chunk }
                        });

                        await response.WriteAsync($"data: {outJson}\n\n");
                        await response.Body.FlushAsync();
                    }

                    var doneJson = JsonSerializer.Serialize(new { type = "done" });
                    await response.WriteAsync($"data: {doneJson}\n\n");
                    await response.Body.FlushAsync();

                    return new StreamResult(assistantBuffer.ToString(), sawText, sawTools);
            }
            
            await foreach (var delta in _aIService.StreamChatWithToolsAsync(messages, originalPayload, ct))
            {
                // Text
                if (!string.IsNullOrEmpty(delta.Text))
                {
                    sawText = true;
                    assistantBuffer.Append(delta.Text);

                    var outJson = JsonSerializer.Serialize(new
                    {
                        type = "data",
                        delta = new { content = delta.Text }
                    });

                    await response.WriteAsync($"data: {outJson}\n\n");
                    await response.Body.FlushAsync();
                }

                // Tool calls
                if (delta.ToolCalls is { Count: > 0 })
                {
                    sawTools = true;

                    var toolJson = JsonSerializer.Serialize(new
                    {
                        type = "tool_calls",
                        tool_calls = delta.ToolCalls
                    });

                    await response.WriteAsync($"data: {toolJson}\n\n");
                    await response.Body.FlushAsync();
                }
            }

            return new StreamResult(assistantBuffer.ToString(), sawText, sawTools);

            }


        #endregion

        #region Message / Prompt Building

        private async Task<string?> SaveLatestUserMessageAsync(
            ChatDto chat,
            JsonElement messagesProp,
            List<ChatMessageDto> chatMessages,
            CancellationToken ct)
        {
            if (messagesProp.ValueKind != JsonValueKind.Array)
                return null;

            JsonElement? lastIncomingUserEl = null;
            var items = messagesProp.EnumerateArray().ToArray();
            var totalCount = items.Length;

            for (int i = items.Length - 1; i >= 0; i--)
            {
                var m = items[i];
                if (m.ValueKind == JsonValueKind.Object &&
                    m.TryGetProperty("role", out var roleProp) &&
                    string.Equals(roleProp.GetString(), "user", StringComparison.OrdinalIgnoreCase))
                {
                    if (lastIncomingUserEl is null)
                        lastIncomingUserEl = m;
                }
            }

            if (lastIncomingUserEl is not JsonElement userMsgEl)
                return null;

            var text = ChatTools.ExtractUserVisibleText(userMsgEl);
            if (string.IsNullOrWhiteSpace(text) || ChatTools.LooksLikeToolJson(text))
                return text;

            // Decide whether to save this as a new user message
            bool definitelySave = totalCount <= 2; // only system+user present
            bool maybeSaveNew = totalCount > 2 && IsDifferentFromLastPersistedUser(chatMessages, text);

            if (definitelySave || maybeSaveNew)
            {
                var userMsg = new ChatMessageDto
                {
                    Id = Guid.NewGuid(),
                    ChatId = chat.Id,
                    Role = "user",
                    Content = text,
                    Timestamp = DateTime.UtcNow,
                    TokensUsed = 0
                };

                await _chatService.SaveChatMessageAsync(userMsg, ct);
                chatMessages.Add(userMsg);
            }

            return text;
        }

        #endregion

        #region Prompt Helpers (System + Documents)

        private async Task<string> GetSystemPromptForChatAsync(Guid chatId, CancellationToken ct)
        {
            var basePrompt = _openAPISettings.DefaultSystemPrompt;

            var systemPrompt = "[SYSTEM_PROMPT_START]\n" + basePrompt + "\n[SYSTEM_PROMPT_END]";

            return systemPrompt;
        }

        private async Task<string> PrepareDocumentsPromptForChat(IList<Guid> documentIds, CancellationToken ct)
        {

            var documents = await _documentService.GetDocumentsByIdsAsync(documentIds, ct);

            if (documents == null || !documents.Any())
                return string.Empty;

            var documentsPrompt = await _documentService.GetPromptAsync(
                documents.Select(d => d.Id).ToList(),
                ct);

            documentsPrompt =
                "\n[DOCUMENTS_CONTEXT_START]\n" +
                documentsPrompt +
                "\n[DOCUMENTS_CONTEXT_END]\n";

            return documentsPrompt;
        }

        #endregion

        #region Small Utilities

        private static async Task WriteBadRequestAsync(HttpResponse response, string message)
        {
            response.StatusCode = StatusCodes.Status400BadRequest;
            response.ContentType = "application/json";
            var json = JsonSerializer.Serialize(new { error = message });
            await response.WriteAsync(json);
        }

        private static bool IsDifferentFromLastPersistedUser(List<ChatMessageDto> history, string incoming)
        {
            var lastPersistedUser = history.LastOrDefault(m =>
                string.Equals(m.Role, "user", StringComparison.OrdinalIgnoreCase));
            if (lastPersistedUser is null) return true;

            return !string.Equals(Normalize(lastPersistedUser.Content), Normalize(incoming), StringComparison.Ordinal);
        }

        private static string Normalize(string s)
        {
            var t = (s ?? string.Empty).Trim().Replace("\r\n", "\n").Replace("\r", "\n");
            return string.Join(" ", t.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries));
        }

        private string BuildTransientSystemPrompt(JsonElement payload)
        {
            var basePrompt = _openAPISettings.DefaultSystemPrompt ?? string.Empty;
            var sb = new StringBuilder(basePrompt);

            if (payload.TryGetProperty("systemPromptOverride", out var overrideProp) &&
                overrideProp.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(overrideProp.GetString()))
            {
                sb.AppendLine();
                sb.AppendLine("[TRANSIENT_SYSTEM_PROMPT_OVERRIDE_START]");
                sb.AppendLine(overrideProp.GetString());
                sb.AppendLine("[TRANSIENT_SYSTEM_PROMPT_OVERRIDE_END]");
            }

            if (payload.TryGetProperty("context", out var contextProp) &&
                contextProp.ValueKind is JsonValueKind.Object or JsonValueKind.Array)
            {
                sb.AppendLine();
                sb.AppendLine("[TRANSIENT_CONTEXT_DATA_START]");
                sb.AppendLine(contextProp.GetRawText());
                sb.AppendLine("[TRANSIENT_CONTEXT_DATA_END]");
            }

            return sb.ToString();
        }

        private static string ExtractLatestUserText(JsonElement messagesProp)
        {
            if (messagesProp.ValueKind != JsonValueKind.Array) return string.Empty;

            var items = messagesProp.EnumerateArray().ToArray();
            for (int i = items.Length - 1; i >= 0; i--)
            {
                var msg = items[i];
                if (msg.ValueKind != JsonValueKind.Object) continue;
                if (!msg.TryGetProperty("role", out var roleProp)) continue;
                if (!string.Equals(roleProp.GetString(), "user", StringComparison.OrdinalIgnoreCase)) continue;

                return ChatTools.ExtractUserVisibleText(msg);
            }

            return string.Empty;
        }

        private static bool TryGetTransientSessionId(JsonElement payload, out Guid sessionId)
        {
            sessionId = Guid.Empty;
            if (!payload.TryGetProperty("sessionId", out var sessionIdProp) ||
                sessionIdProp.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            return Guid.TryParse(sessionIdProp.GetString(), out sessionId) && sessionId != Guid.Empty;
        }

        private string BuildTransientCacheKey(Guid sessionId)
        {
            if (!_userContext.IsAuthenticated || _userContext.UserId == Guid.Empty)
                throw new UnauthorizedAccessException("Transient chat requires an authenticated user.");

            var userKey = _userContext.UserId.ToString("N");
            return $"transient-chat:{userKey}:{sessionId:N}";
        }

        private static void TrimTransientTurns(List<ChatMessageDto> messages)
        {
            if (messages.Count <= TransientMaxTurns) return;

            var removeCount = messages.Count - TransientMaxTurns;
            messages.RemoveRange(0, removeCount);
        }

        #endregion
    }

    internal sealed class TransientChatState
    {
        public List<ChatMessageDto> Messages { get; set; } = new();
    }
}
