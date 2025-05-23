﻿using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace Docplanner.Common.Converters;

public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    private static readonly string[] DateTimeFormats = new[]
    {
        "yyyy-MM-ddTHH:mm:ss",  // GET response format
        "yyyy-MM-dd HH:mm:ss"   // POST request format
    };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateString = reader.GetString();

        if (DateTime.TryParseExact(dateString, DateTimeFormats,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        try
        {
            return DateTime.Parse(dateString!);
        }
        catch (FormatException ex)
        {
            throw new JsonException($"Unable to parse '{dateString}' as a DateTime.", ex);
        }
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}
