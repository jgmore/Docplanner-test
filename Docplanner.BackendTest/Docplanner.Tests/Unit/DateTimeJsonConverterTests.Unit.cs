using System.Text.Json;
using System.Text.Json.Serialization;
using Docplanner.Common.Converters;
using Xunit;

namespace Docplanner.Tests.Unit;

[Trait("Category", "Unit")]
public class DateTimeJsonConverterTests
{
    private readonly JsonSerializerOptions _options;

    public DateTimeJsonConverterTests()
    {
        _options = new JsonSerializerOptions
        {
            Converters = { new DateTimeJsonConverter() }
        };
    }

    public class TestDto
    {
        public DateTime Timestamp { get; set; }
    }

    [Fact]
    public void Read_Parses_ISO8601_With_T_Format()
    {
        var json = "{\"Timestamp\":\"2025-04-25T14:30:00\"}";
        var result = JsonSerializer.Deserialize<TestDto>(json, _options);

        Assert.Equal(new DateTime(2025, 4, 25, 14, 30, 0), result!.Timestamp);
    }

    [Fact]
    public void Read_Parses_SpaceDelimited_Format()
    {
        var json = "{\"Timestamp\":\"2025-04-25 14:30:00\"}";
        var result = JsonSerializer.Deserialize<TestDto>(json, _options);

        Assert.Equal(new DateTime(2025, 4, 25, 14, 30, 0), result!.Timestamp);
    }

    [Fact]
    public void Read_FallsBack_To_Standard_Parse()
    {
        var json = "{\"Timestamp\":\"April 25, 2025 2:30 PM\"}";
        var result = JsonSerializer.Deserialize<TestDto>(json, _options);

        Assert.Equal(new DateTime(2025, 4, 25, 14, 30, 0), result!.Timestamp);
    }

    [Fact]
    public void Write_Uses_SpaceDelimited_Format()
    {
        var dto = new TestDto { Timestamp = new DateTime(2025, 4, 25, 14, 30, 0) };
        var json = JsonSerializer.Serialize(dto, _options);

        Assert.Contains("\"Timestamp\":\"2025-04-25 14:30:00\"", json);
    }

    [Fact]
    public void Read_ThrowsException_When_InvalidDateFormat()
    {
        var json = "{\"Timestamp\":\"Not a date\"}";

        var exception = Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<DateTimeJsonConverterTests.TestDto>(json, _options));

        Assert.Contains("Unable to parse", exception.Message);
    }

}
