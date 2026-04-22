using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedInsights.Lib;

public sealed class NullableDateOnlyConverter : JsonConverter<DateOnly?>
{
    private static readonly string[] Formats = { "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy" };

    public override DateOnly? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string or null for DateOnly.");

        var s = reader.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return null;

        s = s.Trim().Trim('"');

        if (DateOnly.TryParseExact(s, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;

        if (DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            return DateOnly.FromDateTime(dto.DateTime);

        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return DateOnly.FromDateTime(dt);

        throw new JsonException($"Invalid nullable DateOnly format: {s}");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStringValue(value.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }
}
