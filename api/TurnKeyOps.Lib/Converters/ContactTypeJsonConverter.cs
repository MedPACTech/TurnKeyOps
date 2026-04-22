using System.Text.Json;
using System.Text.Json.Serialization;
using MedInsights.Lib.Enums;

namespace MedInsights.Lib.Converters;

public sealed class ContactTypeJsonConverter : JsonConverter<ContactType>
{
    public override ContactType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var numeric))
        {
            return Enum.IsDefined(typeof(ContactType), numeric) ? (ContactType)numeric : ContactType.Other;
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString()?.Trim();
            if (string.IsNullOrWhiteSpace(raw)) return ContactType.Other;

            if (int.TryParse(raw, out var numericFromString))
            {
                return Enum.IsDefined(typeof(ContactType), numericFromString)
                    ? (ContactType)numericFromString
                    : ContactType.Other;
            }

            if (Enum.TryParse<ContactType>(raw, ignoreCase: true, out var parsed))
            {
                return parsed;
            }

            return ContactType.Other;
        }

        throw new JsonException($"Unexpected token {reader.TokenType} while parsing ContactType.");
    }

    public override void Write(Utf8JsonWriter writer, ContactType value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
