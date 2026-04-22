using System;
using Microsoft.AspNetCore.Http;
using MedInsights.Lib.Dtos;

namespace MedInsights.Models
{

    // Requests / Results (key fields)
    public record SummarizeRequest(
        Guid SessionId,
        IReadOnlyList<Turn> Turns,             // ordered oldest→newest
        ConversationSummary? ExistingSummary,  // null on first call, hands previous summary on subsequent calls
        SummarizeStyle Style,                  // Bullets, JSON, Mixed
        int TargetTokens,                      // e.g., 300–800
        bool RedactSensitive                   // config driven
    );

        // Requests / Results (key fields)
    public record SummaryRequest(
        Guid SessionId,
        IReadOnlyList<ChatMessageDto> ChatMessages,             // ordered oldest→newest
        string? ExistingSummary,  // null on first call, hands previous summary on subsequent calls
        SummarizeStyle Style,                  // Bullets, JSON, Mixed
        int TargetTokens,                      // e.g., 300–800
        bool RedactSensitive                   // config driven
    );

    public record AppendRequest(
        Guid SessionId,
        Turn LastUserTurn,
        Turn LastAssistantTurn,
        ConversationSummary ExistingSummary,
        SummarizeStyle Style,
        int TargetTokens,
        bool RedactSensitive
    );

    public record SummaryResult(
        ConversationSummary Summary,           // text or JSON string
        int SourceTurnsCount,
        IReadOnlyList<Guid> CoveredTurnIds,    // what turns are captured
        bool IsLossy,                          // anything dropped?
        int EstimatedTokens                    // summary size
    );

    public record ConversationSummary(
        string Format,                         // "json.v1" | "bullets.v1"
        string Content                         // the actual summary
    );

    // Utility DTOs
    public record Turn(Guid Id, string Role, string Content, DateTimeOffset Ts, ToolMeta? Tool = null);
    public record ToolMeta(string Name, string? CallId, string? InputJson, string? OutputJson);

    public enum SummarizeStyle { Json, Bullets, Mixed }

    public record TopicMapRequest(Guid SessionId, IReadOnlyList<Turn> Turns, int MaxTopics);
    public record TopicMapResult(IReadOnlyList<TopicSummary> Topics);

    public record TopicSummary(string TopicId, string Title, string Summary, IReadOnlyList<Guid> TurnIds);

    public sealed record ChatStreamEvent(string? Token, Guid? ChatId, Guid? SessionId, string? Title, bool IsMeta);

    // 1) In ChatSessionState add:
    // private sealed class ChatSessionState
    // {
    //     public OpenAI.Chat.ChatMessage System { get; }
    //     public List<TurnRec> Turns { get; } = new();
    //     public List<DisplayTurn> Display { get; } = new();
    //     public ConversationSummary? RollingSummary { get; set; }
    //     public string? Title { get; set; }            // <-- NEW
    //     public DateTime LastTouchedUtc { get; set; } = DateTime.UtcNow;
    //     public SemaphoreSlim Gate { get; } = new(1, 1);

    //     public ChatSessionState(string systemPrompt)
    //     {
    //         System = OpenAI.Chat.ChatMessage.CreateSystemMessage(systemPrompt);
    //     }
    // }

    public sealed class Session
    {
        public Guid SessionId { get; }
        public string? System { get; set; }
        public string? Title { get; set; }
        public ConversationSummary? RollingSummary { get; set; }
        public List<TurnRec> Turns { get; } = new();
        public List<DisplayTurn> Display { get; } = new();
        public SemaphoreSlim Gate { get; set; } = new(1, 1);
        public DateTime LastTouchedUtc { get; set; }

        public Session(Guid sessionId)
        {
            SessionId = sessionId;
        }

        public sealed record TurnRec(Guid Id, string Role, string Content, DateTimeOffset Ts);

        public sealed record DisplayTurn(string Role, string Content, DateTimeOffset Ts);

        // public sealed class ConversationSummary
        // {
        //     public string Style { get; }
        //     public string Content { get; set; }

        //     public ConversationSummary(string style, string content)
        //     {
        //         Style = style;
        //         Content = content;
        //     }
        // }



    }

    public class AudioStreamRequest
    {
        public IFormFile File { get; set; }
        public Guid? ChatId { get; set; }
        public Guid? SessionId { get; set; }
        public string? Voice { get; set; }
        public string? Format { get; set; } // "wav" or "mp3"
        //public string? Prompt { get; set; }
    }


    public class SpeakRequest
    {
        /// <summary>
        /// The chat ID to fetch a reply from.
        /// </summary>
        public Guid? ChatId { get; set; }

        /// <summary>
        /// The session ID for the conversation.
        /// </summary>
        public Guid? SessionId { get; set; }

        /// <summary>
        /// Desired TTS voice (e.g., "alloy").
        /// </summary>
        public string? Voice { get; set; }

        /// <summary>
        /// Desired output format (e.g., "wav" or "mp3").
        /// </summary>
        public string? Format { get; set; }
        public string? Prompt { get; set; }
    }
}