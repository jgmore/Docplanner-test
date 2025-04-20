using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

namespace Docplanner.Common.Converters;

public class DateTimeJsonConverter : JsonConverter<DateTime>
{
    private static readonly string[] DateTimeFormats = new[]
    {
        "yyyy-MM-ddTHH:mm:ss",  // Formato en respuesta GET
        "yyyy-MM-dd HH:mm:ss"   // Formato en request POST
    };

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var dateString = reader.GetString();

        // Intentar analizar con varios formatos
        if (DateTime.TryParseExact(dateString, DateTimeFormats,
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var result))
        {
            return result;
        }

        // Si falla, intentar con parse estándar
        return DateTime.Parse(dateString);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Para POST requests usamos el formato "yyyy-MM-dd HH:mm:ss"
        writer.WriteStringValue(value.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}
