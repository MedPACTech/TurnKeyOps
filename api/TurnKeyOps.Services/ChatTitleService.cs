// MedInsights.Services/ChatTitleService.cs
using System.Text;
using MedInsights.Services.Interfaces;
using OpenAI.Chat;


namespace MedInsights.Services;

public sealed class ChatTitleService : IChatTitleService
{
    private readonly IAIService<ChatMessage> _aIService;

    public ChatTitleService(IAIService<ChatMessage> aIService) => _aIService = aIService;

    public async Task<string> GenerateTitleAsync(
        string? previousTitle,
        string lastUser,
        string lastAssistant,
        string? rollingSummary,
        CancellationToken ct = default)
    {

        // Cheap guard rails:
        static string Fallback(string text)
        {
            var s = (text ?? "").Trim().Replace("\r", " ").Replace("\n", " ");
            s = s.Length > 80 ? s[..80] : s;
            // Title Case-ish, no trailing punctuation
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s).TrimEnd('.', ':', '-', '—');
        }

        //TODO: Pull this out of the template prompts service. 
        var sb = new StringBuilder();
        sb.AppendLine("You name chat conversations.");
        sb.AppendLine("Rules:");
        sb.AppendLine("- 3–7 words.");
        sb.AppendLine("- Title Case. No quotes or trailing punctuation.");
        sb.AppendLine("- Use proper nouns only if they appear in messages.");
        sb.AppendLine("- If the new topic is basically the same as the previous, keep the previous title.");
        sb.AppendLine("- Never return an empty title, use a fallback such as the last user message.");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(previousTitle))
            sb.AppendLine($"Previous Title: {previousTitle}");
        if (!string.IsNullOrWhiteSpace(rollingSummary))
            sb.AppendLine($"Context Summary: {rollingSummary}");
        sb.AppendLine($"Last User: {lastUser}");
        //sb.AppendLine($"Last Assistant: {lastAssistant}");
        sb.AppendLine();
        sb.Append("Return only the title.");

        try
        {
            var systemMessage = sb.ToString();
            var userMessage = new List<string>
            {
                "Generate the title now."
            };

            //Use low level chat model
            //TODO: Configurable model selection create configuration 
            _aIService.SetServiceModel("gpt-4o-mini");
            var text = await _aIService.GetChatCompletionAsync(systemMessage, userMessage, 580, 0.1f, ct);
            var title = (text ?? "").Trim();

            // Fallbacks for weird/empty outputs
            if (string.IsNullOrWhiteSpace(title)) title = previousTitle ?? Fallback(lastUser);
            return title;
        }
        catch
        {
            return previousTitle ?? Fallback(lastUser);
        }
    }
}
