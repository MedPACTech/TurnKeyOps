using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using MedInsights.Lib.Configurations;
using MedInsights.Services.Interfaces;
using MedInsights.Lib.Dtos;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Text;

namespace MedInsights.Services
{
    public sealed record AiDelta(string? Text, IReadOnlyList<object>? ToolCalls);

    //THIS IS THE GATE KEEPER FOR ALL OPENAI CALLS, it handles required models and interactions
    public sealed class OpenAIService : IAIService<ChatMessage>
    {
        private readonly OpenAIClient _client;
        private readonly string _openApiKey; //Key is set at Program.cs level via DI
        private string _model;

        public OpenAIService(IOptions<OpenAISettings> openAISettings, OpenAIClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _model = string.IsNullOrWhiteSpace(openAISettings.Value.DefaultModel)
                ? throw new ArgumentNullException("default openai model not set")
                : openAISettings.Value.DefaultModel;
            _openApiKey = openAISettings.Value.Key;
        }

        public List<ChatMessage> BuildChatMessages(
            string systemPrompt,
            string documentsPrompt,
            string summary,
            List<ChatMessageDto> chatMessages,
            JsonElement payload)
        {
            var messages = new List<ChatMessage>();

            // 0️⃣ System / docs / summary
            if (!string.IsNullOrWhiteSpace(systemPrompt))
                messages.Add(ChatMessage.CreateSystemMessage(systemPrompt));

            if (!string.IsNullOrWhiteSpace(documentsPrompt))
                messages.Add(ChatMessage.CreateSystemMessage(documentsPrompt));

            if (!string.IsNullOrWhiteSpace(summary))
                messages.Add(ChatMessage.CreateSystemMessage($"Previous summary context:\n{summary}"));

            // 1️⃣ Persisted history
            AddHistoryMessages(messages, chatMessages);

            // 2️⃣ Incoming payload messages (includes tools)
            AddPayloadMessages(messages, payload);

            return messages;
        }

        /// <summary>
        /// Legacy text-only streaming (not used at the moment).
        /// Kept for compatibility; tool-aware path is StreamChatWithToolsAsync.
        /// </summary>
        public async IAsyncEnumerable<string> StreamChatAsync(
            IEnumerable<ChatMessage> messages,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var chatOptions = new ChatCompletionOptions { MaxOutputTokenCount = 1536 };
            var chatClient = _client.GetChatClient(_model);
            var stream = chatClient.CompleteChatStreamingAsync(messages, chatOptions, ct);

            await foreach (var update in stream.WithCancellation(ct))
            {
                if (update?.ContentUpdate is { Count: > 0 })
                {
                    var chunk = string.Concat(update.ContentUpdate.Select(p => p.Text));
                    if (chunk.Length > 0)
                        yield return chunk;
                }
            }
        }

        public async IAsyncEnumerable<AiDelta> StreamChatWithToolsAsync(
            IEnumerable<ChatMessage> messages,
            JsonElement originalPayload,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var options = BuildChatOptionsFromPayload(originalPayload);

            var chatClient = _client.GetChatClient(_model);
            var stream = chatClient.CompleteChatStreamingAsync(messages, options, ct);

            // 🔧 Keeps tool call state across updates
            var toolCallState = new Dictionary<string, ToolCallAccumulator>();
            string? lastToolCallKey = null;

            await foreach (var update in stream.WithCancellation(ct))
            {
                if (update is null)
                    continue;

                // 1️⃣ Text deltas → stream them out as they arrive
                if (update.ContentUpdate is { Count: > 0 })
                {
                    var textChunk = string.Concat(update.ContentUpdate.Select(p => p.Text));
                    if (!string.IsNullOrEmpty(textChunk))
                    {
                        yield return new AiDelta(textChunk, null);
                    }
                }

                // 2️⃣ Tool call deltas → accumulate across updates
                if (update.ToolCallUpdates is { Count: > 0 })
                {
                    foreach (var toolUpdate in update.ToolCallUpdates)
                    {
                        var rawId = toolUpdate.ToolCallId;
                        var rawName = toolUpdate.FunctionName;

                        // Decide which key to use
                        string key;
                        if (!string.IsNullOrEmpty(rawId))
                        {
                            key = rawId;
                            lastToolCallKey = key;
                        }
                        else if (!string.IsNullOrEmpty(rawName))
                        {
                            key = $"fn::{rawName}";
                            lastToolCallKey = key;
                        }
                        else if (!string.IsNullOrEmpty(lastToolCallKey))
                        {
                            // No id and no name → continuation of the last tool call
                            key = lastToolCallKey;
                        }
                        else
                        {
                            key = $"tool_call::{toolCallState.Count}";
                            lastToolCallKey = key;
                        }

                        if (!toolCallState.TryGetValue(key, out var acc))
                        {
                            acc = new ToolCallAccumulator(rawName ?? string.Empty);
                            toolCallState[key] = acc;
                        }
                        else if (!string.IsNullOrEmpty(rawName))
                        {
                            acc.FunctionName = rawName;
                        }

                        // FunctionArgumentsUpdate is BinaryData → convert to string
                        if (toolUpdate.FunctionArgumentsUpdate is { } argData)
                        {
                            var argsChunk = argData.ToString();
                            if (!string.IsNullOrEmpty(argsChunk))
                            {
                                acc.Arguments.Append(argsChunk);
                            }
                        }
                    }
                }

                // 3️⃣ Check finish reason directly on the update
                var finished = update.FinishReason is ChatFinishReason.ToolCalls
                               or ChatFinishReason.Stop;

                // 4️⃣ If finished AND we have tool calls, emit them once & end stream
                if (finished && toolCallState.Count > 0)
                {
                    var toolCalls = toolCallState
                        .Select(kvp => (object)new
                        {
                            id = kvp.Key,
                            type = "function",
                            function = new
                            {
                                name = kvp.Value.FunctionName,
                                arguments = kvp.Value.Arguments.ToString()
                            }
                        })
                        .ToList();

                    yield return new AiDelta(null, toolCalls);
                    yield break;
                }

                // 5️⃣ Finished with no tools (pure text completion) → just end
                if (finished && toolCallState.Count == 0)
                {
                    yield break;
                }
            }
        }

        /// <summary>
        /// Sets the current model to be used in Open API Calls, always defaults to the base model if not set. 
        /// Works in instance only. 
        /// </summary>
        public void SetServiceModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
                throw new ArgumentException("Model cannot be null or empty", nameof(model));
            _model = model;
        }


        //"GPT-5-nano" in this sdk does not use temp. 
        public async Task<string> GetChatCompletionAsync(
            string systemPrompt,
            IEnumerable<string> userMessages,
            int maxOutputTokens,
            double temperature = 0.1,
            CancellationToken ct = default)
        {
            var model = _model;

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(systemPrompt)
            };

            messages.AddRange(userMessages.Select(ChatMessage.CreateUserMessage));

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = maxOutputTokens,
                Temperature = (float)temperature
            };

            var chatClient = _client.GetChatClient(model);
            var response = await chatClient.CompleteChatAsync(messages, options, ct);

            return response.Value.Content.FirstOrDefault()?.Text ?? string.Empty;
        }

        private ChatCompletionOptions BuildChatOptionsFromPayload(JsonElement payload)
        {
            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 1536
            };

            // model override (optional)
            if (payload.TryGetProperty("model", out var modelProp) &&
                modelProp.ValueKind == JsonValueKind.String)
            {
                var modelOverride = modelProp.GetString();
                if (!string.IsNullOrWhiteSpace(modelOverride))
                    _model = modelOverride!;
            }

            // temperature (optional)
            if (payload.TryGetProperty("temperature", out var tempProp) &&
                tempProp.ValueKind is JsonValueKind.Number &&
                tempProp.TryGetDouble(out var temp))
            {
                options.Temperature = (float)temp;
            }

            // tools (critical)
            if (payload.TryGetProperty("tools", out var toolsProp) &&
                toolsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var toolEl in toolsProp.EnumerateArray())
                {
                    // Supported shapes:
                    // 1) OpenAI-style:
                    //    { "type":"function", "function": { "name":"...", "description":"...", "parameters":{...} } }
                    // 2) Simplified:
                    //    { "name":"...", "description":"...", "parameters":{...} }
                    string? fnName = null;
                    string? fnDesc = null;
                    JsonElement? parameters = null;

                    if (toolEl.TryGetProperty("function", out var fnProp))
                    {
                        if (toolEl.TryGetProperty("type", out var typeProp) &&
                            !string.Equals(typeProp.GetString(), "function", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        fnName = fnProp.TryGetProperty("name", out var nameProp)
                            ? nameProp.GetString()
                            : null;
                        fnDesc = fnProp.TryGetProperty("description", out var descProp)
                            ? descProp.GetString()
                            : null;
                        if (fnProp.TryGetProperty("parameters", out var paramProp))
                            parameters = paramProp;
                    }
                    else
                    {
                        fnName = toolEl.TryGetProperty("name", out var nameProp)
                            ? nameProp.GetString()
                            : null;
                        fnDesc = toolEl.TryGetProperty("description", out var descProp)
                            ? descProp.GetString()
                            : null;
                        if (toolEl.TryGetProperty("parameters", out var paramProp))
                            parameters = paramProp;
                    }

                    if (string.IsNullOrWhiteSpace(fnName))
                        continue;

                    var tool = ChatTool.CreateFunctionTool(
                        fnName!,
                        fnDesc ?? string.Empty,
                        parameters.HasValue ? BinaryData.FromString(parameters.Value.GetRawText()) : null);

                    options.Tools.Add(tool);
                }
            }

            // tool_choice (optional)
            if (payload.TryGetProperty("tool_choice", out var choiceProp) &&
                choiceProp.ValueKind == JsonValueKind.String)
            {
                var choice = choiceProp.GetString();
                // TODO: map "auto" | "none" | specific function name to options.ToolChoice
                // e.g. options.ToolChoice = ChatToolChoice.Auto; etc.
            }

            return options;
        }

        /// <summary>
        /// Map persisted DTO history into ChatMessage list.
        /// </summary>
        private static void AddHistoryMessages(
            List<ChatMessage> messages,
            List<ChatMessageDto> chatMessages)
        {
            foreach (var m in chatMessages)
            {
                var content = m.Content ?? string.Empty;
                switch (m.Role?.ToLowerInvariant())
                {
                    case "user":
                        messages.Add(ChatMessage.CreateUserMessage(content));
                        break;
                    case "assistant":
                        messages.Add(ChatMessage.CreateAssistantMessage(content));
                        break;
                    case "system":
                        messages.Add(ChatMessage.CreateSystemMessage(content));
                        break;
                }
            }
        }

        /// <summary>
        /// Map incoming JSON payload messages (including tools) into ChatMessage list.
        /// </summary>
        private static void AddPayloadMessages(
            List<ChatMessage> messages,
            JsonElement payload)
        {
            if (!payload.TryGetProperty("messages", out var msgArray) ||
                msgArray.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            foreach (var msg in msgArray.EnumerateArray())
            {
                if (!msg.TryGetProperty("role", out var roleProp))
                    continue;

                var role = roleProp.GetString() ?? string.Empty;

                // 🧩 Assistant with tool_calls
                if (string.Equals(role, "assistant", StringComparison.OrdinalIgnoreCase) &&
                    msg.TryGetProperty("tool_calls", out var toolCallsProp) &&
                    toolCallsProp.ValueKind == JsonValueKind.Array)
                {
                    var toolCalls = new List<ChatToolCall>();

                    foreach (var tc in toolCallsProp.EnumerateArray())
                    {
                        if (!tc.TryGetProperty("type", out var typeProp) ||
                            !string.Equals(typeProp.GetString(), "function", StringComparison.OrdinalIgnoreCase) ||
                            !tc.TryGetProperty("function", out var fnProp))
                        {
                            continue;
                        }

                        var id = tc.TryGetProperty("id", out var idProp)
                            ? idProp.GetString()
                            : null;

                        var fnName = fnProp.TryGetProperty("name", out var nameProp)
                            ? nameProp.GetString()
                            : null;

                        var argsRaw = fnProp.TryGetProperty("arguments", out var argsProp)
                            ? argsProp.GetString()
                            : null;

                        if (string.IsNullOrWhiteSpace(fnName))
                            continue;

                        // ✅ Create function tool call with BinaryData arguments
                        var toolCall = ChatToolCall.CreateFunctionToolCall(
                            id ?? Guid.NewGuid().ToString("N"),
                            fnName!,
                            BinaryData.FromString(argsRaw ?? "{}"));

                        toolCalls.Add(toolCall);
                    }

                    if (toolCalls.Count > 0)
                    {
                        // ✅ Assistant message containing tool calls
                        var assistantWithTools = ChatMessage.CreateAssistantMessage(toolCalls);
                        messages.Add(assistantWithTools);
                        continue;
                    }
                    // If no toolCalls added, fall through to normal assistant handling
                }

                // 🧩 Tool message (tool result)
                if (string.Equals(role, "tool", StringComparison.OrdinalIgnoreCase))
                {
                    if (!msg.TryGetProperty("tool_call_id", out var idProp))
                        continue;

                    var toolCallId = idProp.GetString();
                    if (string.IsNullOrWhiteSpace(toolCallId))
                        continue;

                    var contentJson = msg.TryGetProperty("content", out var contentProp)
                        ? contentProp.GetString() ?? string.Empty
                        : string.Empty;

                    var name = msg.TryGetProperty("name", out var nameProp2)
                        ? nameProp2.GetString()
                        : null;

                    // ✅ Use string content here
                    var toolMessage = ChatMessage.CreateToolMessage(toolCallId, name, contentJson);

                    messages.Add(toolMessage);
                    continue;
                }

                // 🧩 Regular text messages
                if (!msg.TryGetProperty("content", out var contentProp2) ||
                    (contentProp2.ValueKind != JsonValueKind.String &&
                    contentProp2.ValueKind != JsonValueKind.Null))
                {
                    continue;
                }

                var contentText = contentProp2.GetString() ?? string.Empty;

                switch (role.ToLowerInvariant())
                {
                    case "user":
                        messages.Add(ChatMessage.CreateUserMessage(contentText));
                        break;
                    case "assistant":
                        messages.Add(ChatMessage.CreateAssistantMessage(contentText));
                        break;
                    case "system":
                        messages.Add(ChatMessage.CreateSystemMessage(contentText));
                        break;
                }
            }
        }


        private sealed class ToolCallAccumulator
        {
            public string FunctionName { get; set; }
            public StringBuilder Arguments { get; }

            public ToolCallAccumulator(string functionName)
            {
                FunctionName = functionName;
                Arguments = new StringBuilder();
            }
        }
    }
}
