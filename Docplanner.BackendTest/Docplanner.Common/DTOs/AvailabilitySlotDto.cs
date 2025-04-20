using System.Text.Json.Serialization;
using Docplanner.Common.Converters;

namespace Docplanner.Common.DTOs;

public class AvailabilitySlotDto
{
    [JsonConverter(typeof(DateTimeJsonConverter))]
    public DateTime Start { get; set; }

    [JsonConverter(typeof(DateTimeJsonConverter))]
    public DateTime End { get; set; }

    public string DayOfWeek { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
}
