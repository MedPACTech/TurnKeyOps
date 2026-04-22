using MedInsights.Models;

namespace MedInsights.Services;

/// <summary>Windowing policy: thresholds and builder for the role/content list sent to the model.</summary>
internal static class ChatWindowing
{
    // // When total chat chars exceed this, we trigger summarization.
    // public const int MaxCharsHistory = 20_000;

    // // Budget for the window we actually send (summary + recent turns).
    // public const int MaxCharsWindow  = 12_000;

    // /// <summary>Rough estimate: include small overhead per message.</summary>
    // public static int EstimateChars(IEnumerable<ChatMessage> msgs)
    //     => msgs.Sum(m => (m.Content?.Length ?? 0) + 20);

    // /// <summary>
    // /// Build: system + optional title + optional summary + recent turns (both roles).
    // /// IMPORTANT: This assumes the latest user message has already been appended to chat.Messages.
    // /// </summary>
    // public static IEnumerable<(string role, string content)> BuildWindow(Chat chat, string systemPrompt)
    // {
    //     var window = new List<(string role, string content)>
    //     {
    //         ("system", systemPrompt)
    //     };

    //     if (!string.IsNullOrWhiteSpace(chat.Title))
    //         window.Add(("system", $"Topic: {chat.Title}"));

    //     if (!string.IsNullOrWhiteSpace(chat.Summary.Text))
    //         window.Add(("system", $"Summary so far: {chat.Summary.Text}"));

    //     // Walk backward adding recent turns within budget
    //     var recent = new List<ChatMessage>();
    //     var budget = 0;

    //     for (int i = chat.Messages.Count - 1; i >= 0; i--)
    //     {
    //         var m = chat.Messages[i];
    //         var len = (m.Content?.Length ?? 0) + 20;
    //         if (budget + len > MaxCharsWindow) break;
    //         recent.Add(m);
    //         budget += len;
    //     }

    //     recent.Reverse();

    //     foreach (var m in recent)
    //     {
    //         var role = m.Role?.ToLowerInvariant() switch
    //         {
    //             "assistant" => "assistant",
    //             "system"    => "system",
    //             _           => "user"
    //         };
    //         window.Add((role, m.Content ?? string.Empty));
    //     }

    //     return window;
    // }
}
