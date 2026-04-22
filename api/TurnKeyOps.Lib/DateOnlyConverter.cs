using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MedInsights.Lib;

public sealed class DateOnlyConverter : JsonConverter<DateOnly>
{
    private static readonly string[] Formats = { "yyyy-MM-dd", "MM/dd/yyyy", "M/d/yyyy" };

    public override DateOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Expected string for DateOnly.");

        var s = reader.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return default;

        s = s.Trim().Trim('"');

        if (DateOnly.TryParseExact(s, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return d;

        // last-resort: lenient parse
        if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return DateOnly.FromDateTime(dt);

        throw new JsonException($"Invalid DateOnly format: {s}");
    }

    public override void Write(Utf8JsonWriter writer, DateOnly value, JsonSerializerOptions options)
        => writer.WriteStringValue(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)); // normalize output
}
