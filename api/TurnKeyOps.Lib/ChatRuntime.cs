using MedInsights.Models;
using MedInsights.Lib.Dtos;

namespace MedInsights.Lib;

public sealed record StreamDelta(string Type, string Json); 
// Type: "meta" | "data" | "tool_call" | "done" | "error"
// Json: already-serialized JSON payload for the event

public sealed record StreamResult(string AssistantText, bool SawText, bool SawToolCalls);

public interface IEventStreamWriter
{
    // Write one SSE event (already formatted as "data: {json}\n\n" by the writer).
    Task WriteAsync(StreamDelta delta, CancellationToken ct = default);
}

public interface IOpenAIChatClient
{
    // Sends the raw payload stream to OpenAI and returns a line-by-line "data:" stream.
    Task StreamAsync(Stream payload, Func<string,Task> onLine, CancellationToken ct);
}

public interface IChatPostProcessor
{
    Task HandleAsync(Guid chatId,
                     IReadOnlyList<ChatMessageDto> priorMessages,
                     string existingSummary,
                     string assistantText,
                     CancellationToken ct);
}
