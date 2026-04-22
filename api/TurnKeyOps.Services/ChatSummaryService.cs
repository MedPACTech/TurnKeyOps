using System.Text.Json;
using MedInsights.Lib.Configurations;
using MedInsights.Models;
using MedInsights.Services.Interfaces;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace MedInsights.Services
{
    public sealed class ChatSummaryService : IChatSummaryService
    {
        private readonly IAIService<ChatMessage> _aIService;
        private readonly SummarizerSettings _settings;
        private readonly SummarizerPromptTemplates _prompts;

        public ChatSummaryService(IAIService<ChatMessage> aIService, IOptions<SummarizerSettings> settings, IOptions<SummarizerPromptTemplates> prompts)
        {
            _aIService = aIService ?? throw new ArgumentNullException(nameof(aIService));
            _settings = settings?.Value ?? throw new ArgumentNullException(nameof(settings));
            _prompts = prompts?.Value ?? throw new ArgumentNullException(nameof(prompts));
        }

        private void ValidateConfiguration()
        {
            if (string.IsNullOrWhiteSpace(_settings.Model))
                throw new InvalidOperationException("Summarizer model not configured.");
        }

        public async Task<JsonDocument> SummarizeAsync(SummaryRequest request, CancellationToken ct)
        {
            if (request.ChatMessages is null || request.ChatMessages.Count == 0)
                throw new ArgumentException("No messages to summarize.");

            ValidateConfiguration();

            var model = _settings.Model;
            var style = _settings.Style;
            var targetTokens = _settings.TargetTokens > 0 ? _settings.TargetTokens : 400;

            // Select prompt template
            var template = style switch
            {
                SummarizeStyle.Json => _prompts.Json,
                SummarizeStyle.Bullets => _prompts.Bullets,
                SummarizeStyle.Mixed => _prompts.Mixed,
                _ => throw new InvalidOperationException("Unsupported summarize style.")
            };

            // Build conversation subset
            var recent = request.ChatMessages
                .TakeLast(_settings.KeepRecentTurns)
                .Select(t => new { t.Role, t.Content, t.Timestamp })
                .ToList(); //May need to check ordering

            var transcript = JsonSerializer.Serialize(recent);

            // Build system instructions (config-driven)
            var systemPrompt = $"{_prompts.Base}\n\n{template.Replace("{TARGET_TOKENS}", targetTokens.ToString())}";

            // Optional: include previous summary for continuity
            var previousSummaryText = request.ExistingSummary ?? null;
            var continuityPrompt = string.IsNullOrWhiteSpace(previousSummaryText)
                ? "No previous summary available."
                : $"Here is the previous summary to preserve continuity:\n{previousSummaryText}";

            var summaryText = await CompleteAsync(model, systemPrompt, continuityPrompt, transcript, targetTokens, ct);

            var result = new
            {
                recentTurns = recent,
                summary = new
                {
                    text = summaryText.Trim(),
                    previousSummaryIncluded = !string.IsNullOrWhiteSpace(previousSummaryText)
                }
            };

            return JsonDocument.Parse(JsonSerializer.Serialize(result));
        }

        //TODO: temperature param needs to be configurable
        private async Task<string> CompleteAsync(string model, string systemPrompt, string continuityPrompt, string transcript, int targetTokens, CancellationToken ct, double temperature = 0.1)
        {
            // Messages to model
            var userMessages = new List<string>
            {
                continuityPrompt,
                $"Summarize the following JSON transcript:\n{transcript}"
            };

            var maxOutputTokenCount = Math.Clamp(targetTokens + 200, 200, 1200);
            var temperatureValue = (float)temperature;
     
            _aIService.SetServiceModel(model);
            var resp = await _aIService.GetChatCompletionAsync(systemPrompt, userMessages, maxOutputTokenCount, temperatureValue, ct);
            return resp;
        }

    }
}
