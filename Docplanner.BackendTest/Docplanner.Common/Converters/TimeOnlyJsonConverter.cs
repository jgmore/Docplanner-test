using System.Text.Json;
using System.Text.Json.Serialization;

namespace Docplanner.Common.Converters;

public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    private const string TimeFormat = "HH:mm";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return TimeOnly.ParseExact(value!, TimeFormat);
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString(TimeFormat));
    }
}
