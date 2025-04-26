using Docplanner.Common.DTOs;
using Docplanner.Infrastructure.Adapters;
using System.Globalization;
using Xunit;

namespace Docplanner.Tests.Unit;

public class MockSlotServiceAdapterTests
{
    private readonly MockSlotServiceAdapter _adapter;

    public MockSlotServiceAdapterTests()
    {
        _adapter = new MockSlotServiceAdapter();
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_ValidDate_ReturnsCorrectSlots()
    {
        // Arrange
        var monday = "20250421"; // April 21, 2025 (Monday)

        // Act
        var result = await _adapter.FetchWeeklyAvailabilityAsync(monday);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Id1", result.FacilityId);
        
        var slots = result.AvailableSlots.ToList();
        Assert.Equal(2, slots.Count);

        var expectedStart = DateTime.ParseExact(monday, "yyyyMMdd", CultureInfo.InvariantCulture).AddHours(10);

        var firstSlot = slots[0];
        Assert.Equal(expectedStart, firstSlot.Start);
        Assert.Equal(expectedStart.AddMinutes(20), firstSlot.End);
        Assert.Equal("Monday", firstSlot.DayOfWeek);
        Assert.True(firstSlot.IsAvailable);

        var secondSlot = slots[1];
        Assert.Equal(expectedStart.AddMinutes(30), secondSlot.Start);
        Assert.Equal(expectedStart.AddMinutes(50), secondSlot.End);
    }

    [Theory]
    [InlineData("2025-04-21")] // Wrong format (yyyy-MM-dd)
    [InlineData("21-04-2025")] // Wrong format (dd-MM-yyyy)
    [InlineData("")]           // Empty string
    [InlineData(null)]         // Null
    [InlineData("20250432")]    // Invalid day (April 32nd)
    [InlineData("   ")]         // Whitespace
    public async Task FetchWeeklyAvailabilityAsync_InvalidDates_ThrowsArgumentException(string invalidDate)
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _adapter.FetchWeeklyAvailabilityAsync(invalidDate));
    }



    [Fact]
    public async Task TakeSlotAsync_NullRequest_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _adapter.TakeSlotAsync(null));
    }

    [Fact]
    public async Task TakeSlotAsync_MissingStartOrEnd_ReturnsError()
    {
        // Arrange
        var request = new BookingRequestDto
        {
            Start = "",
            End = "2025-04-21T10:20:00",
            Patient = new PatientDto
            {
                Name = "John",
                SecondName = "Doe",
                Email = "john@example.com"
            }
        };

        // Act
        var result = await _adapter.TakeSlotAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid slot times", result.Message);
        Assert.Contains("Start and End times are required", result.Errors);
    }

    [Fact]
    public async Task TakeSlotAsync_MissingPatient_ReturnsError()
    {
        // Arrange
        var request = new BookingRequestDto
        {
            Start = "2025-04-21T10:00:00",
            End = "2025-04-21T10:20:00",
            Patient = null
        };

        // Act
        var result = await _adapter.TakeSlotAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Patient information is required", result.Message);
        Assert.Contains("Patient details are missing", result.Errors);
    }

    [Fact]
    public async Task TakeSlotAsync_MissingPatientDetails_ReturnsError()
    {
        // Arrange
        var request = new BookingRequestDto
        {
            Start = "2025-04-21T10:00:00",
            End = "2025-04-21T10:20:00",
            Patient = new PatientDto
            {
                Name = "",
                SecondName = null,
                Email = ""
            }
        };

        // Act
        var result = await _adapter.TakeSlotAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid patient information", result.Message);
        Assert.Contains("Patient name, second name and email are required", result.Errors);
    }

    [Fact]
    public async Task TakeSlotAsync_ValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new BookingRequestDto
        {
            Start = "2025-04-21T10:00:00",
            End = "2025-04-21T10:20:00",
            Patient = new PatientDto
            {
                Name = "Alice",
                SecondName = "Smith",
                Email = "alice@example.com"
            }
        };

        // Act
        var result = await _adapter.TakeSlotAsync(request);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Id1", result.FacilityId);
        Assert.True(result.Data);
        Assert.Equal("Mock booking confirmed", result.Message);
    }

    [Fact]
    public async Task TakeSlotAsync_MissingAllFields_ReturnsError()
    {
        // Arrange
        var request = new BookingRequestDto
        {
            Start = null,
            End = null,
            Patient = null
        };

        // Act
        var result = await _adapter.TakeSlotAsync(request);

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Invalid slot times", result.Message); // Since it first checks Start/End
        Assert.Contains("Start and End times are required", result.Errors);
    }

}
