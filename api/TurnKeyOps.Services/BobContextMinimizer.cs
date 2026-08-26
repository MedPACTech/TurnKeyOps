using System.Text.Json;
using System.Text.Json.Nodes;
using TurnKeyOps.Services.Interfaces;

namespace TurnKeyOps.Services;

public sealed class BobContextMinimizer : IBobContextMinimizer
{
    private static readonly string[] SensitiveKeyParts =
    [
        "authorization", "password", "passcode", "secret", "token", "apikey", "api_key", "connectionstring",
        "email", "phone", "mobile", "address", "street", "postal", "zipcode", "zip_code", "contact"
    ];

    public JsonElement Minimize(object? value, int maxCharacters = 8_000)
    {
        var node = JsonSerializer.SerializeToNode(value);
        Redact(node);
        var json = node?.ToJsonString() ?? "null";
        if (json.Length > Math.Max(maxCharacters, 256))
        {
            json = JsonSerializer.Serialize(new
            {
                truncated = true,
                summary = json[..Math.Max(maxCharacters - 80, 176)]
            });
        }

        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                var normalized = property.Key.Replace("-", string.Empty).ToLowerInvariant();
                if (SensitiveKeyParts.Any(normalized.Contains))
                    obj[property.Key] = "[REDACTED]";
                else
                    Redact(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array)
                Redact(item);
        }
    }
}
