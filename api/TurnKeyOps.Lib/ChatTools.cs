// --- helpers ---

using System.Text.Json;

namespace MedInsights.Lib;

public static class ChatTools
{

    public static string ExtractUserVisibleText(JsonElement messageObj)
    {
        if (!messageObj.TryGetProperty("content", out var contentProp))
            return string.Empty;

        // Case 1: "content": "plain text"
        if (contentProp.ValueKind == JsonValueKind.String)
            return contentProp.GetString() ?? string.Empty;

        // Case 2: "content": [ { "type": "text", "text": "..." }, ... ]
        if (contentProp.ValueKind == JsonValueKind.Array)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var part in contentProp.EnumerateArray())
            {
                if (part.ValueKind == JsonValueKind.Object &&
                    part.TryGetProperty("type", out var typeProp) &&
                    string.Equals(typeProp.GetString(), "text", StringComparison.OrdinalIgnoreCase) &&
                    part.TryGetProperty("text", out var textProp) &&
                    textProp.ValueKind == JsonValueKind.String)
                {
                    sb.Append(textProp.GetString());
                }
            }
            return sb.ToString();
        }

        // Anything else (objects, toolcalls, etc.) is not user-visible text
        return string.Empty;
    }

    public static bool LooksLikeToolJson(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.TrimStart();
        // cheap guard: raw structured tool payloads (success/message/data...) 
        return t.StartsWith("{") && (t.Contains("\"success\"") || t.Contains("\"tool\"") || t.Contains("\"message\"") && t.Contains("\"data\""));
    }


    public static Guid? TryGetUserMessageId(JsonElement message)
    {
        if (message.TryGetProperty("metadata", out var meta) &&
            meta.ValueKind == JsonValueKind.Object &&
            meta.TryGetProperty("userMessageId", out var idProp) &&
            idProp.ValueKind == JsonValueKind.String &&
            Guid.TryParse(idProp.GetString(), out var id))
        {
            return id;
        }
        return null;
    }
}
