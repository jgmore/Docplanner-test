using Docplanner.Application.Interfaces;
using Docplanner.Application.Services;
using Docplanner.Common.DTOs;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Globalization;

namespace Docplanner.Tests.Unit;

public class SlotServiceTests
{
    private readonly Mock<ISlotServiceAdapter> _adapterMock = new();
    private readonly Mock<ILogger<SlotService>> _loggerMock = new();

    private SlotService CreateService() =>
        new(_adapterMock.Object, _loggerMock.Object);

    [Fact]
    public async Task GetWeeklyAvailability_ShouldReturnSlots()
    {
        var mondayDate = DateTime.ParseExact("20250415", "yyyyMMdd", CultureInfo.InvariantCulture);
        DataResponseDto dataResponse = new DataResponseDto();
        var mockSlots = new List<AvailabilitySlotDto>
        {
            new()
            {
                Start = mondayDate.AddHours(10),
                End = mondayDate.AddHours(10).AddMinutes(20),
                DayOfWeek = "Monday",
                IsAvailable = true
            }
        };
        dataResponse.AvailableSlots = mockSlots;
        dataResponse.FacilityId = "Id1";

        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync("20250415"))
            .ReturnsAsync(dataResponse);

        var service = CreateService();

        // Act
        var result = await service.GetWeeklyAvailabilityAsync("20250415");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Data);
        Assert.Contains(result.Data, slot => slot.Start == mondayDate.AddHours(10));
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_ReturnsEmpty_WhenNoSlotsAvailable()
    {
        DataResponseDto dataResponse = new DataResponseDto();
        dataResponse.AvailableSlots = Array.Empty<AvailabilitySlotDto>();
        dataResponse.FacilityId = "Id1";
        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync("20250415"))
                    .ReturnsAsync(dataResponse);

        var service = CreateService();

        // Act
        var result = await service.GetWeeklyAvailabilityAsync("20250415");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_Throws_WhenAdapterThrowsException()
    {
        // Arrange
        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync("20250415"))
                   .ThrowsAsync(new InvalidOperationException("Simulated adapter failure"));

        var service = CreateService();

        // Act
        var result = await service.GetWeeklyAvailabilityAsync("20250415");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Error retrieving availability", result.Message);
        Assert.Contains("Simulated adapter failure", result.Errors.FirstOrDefault());
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_LogsInformation_WhenCalled()
    {
        DataResponseDto dataResponse = new DataResponseDto();        
        dataResponse.AvailableSlots = new[] { new AvailabilitySlotDto { Start = new DateTime(2025, 4, 15, 09, 00, 00), End = new DateTime(2025, 4, 15, 09, 20, 00) } };
        dataResponse.FacilityId = "Id1";

        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync("20250415"))
                   .ReturnsAsync(dataResponse);

        var service = CreateService();

        // Act
        var result = await service.GetWeeklyAvailabilityAsync("20250415");

        // Assert
        Assert.Single(result.Data);
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Fetching availability for week")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }



    [Fact]
    public async Task BookSlot_ShouldReturnSuccess_WhenBookingSucceeds()
    {
        _adapterMock.Setup(a => a.TakeSlotAsync(It.IsAny<BookingRequestDto>()))
            .ReturnsAsync(ApiResponseDto<bool>.CreateSuccess("Id1", true, "Booked successfully"));

        var service = CreateService();

        var bookingRequest = new BookingRequestDto
        {
            Start = "2025-04-15 10:00:00",
            End = "2025-04-15 10:20:00",
            Comments = "Test booking",
            Patient = new PatientDto
            {
                Name = "Test",
                SecondName = "User",
                Email = "test@example.com",
                Phone = "123456789"
            }
        };

        // Act
        var result = await service.BookSlotAsync(bookingRequest);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Booked successfully", result.Message);
    }

    [Fact]
    public async Task BookSlot_ShouldReturnError_WhenPatientDataInvalid()
    {
        _adapterMock.Setup(a => a.TakeSlotAsync(It.IsAny<BookingRequestDto>()))
            .ReturnsAsync(ApiResponseDto<bool>.CreateError(
                "Invalid patient information",
                new[] { "Patient details are incomplete" }));

        var service = CreateService();

        var bookingRequest = new BookingRequestDto
        {
            Start = "2025-04-15 10:00:00",
            End = "2025-04-15 10:20:00",
            // Paciente incompleto
            Patient = new PatientDto
            {
                Email = "test@example.com"
            }
        };

        // Act
        var result = await service.BookSlotAsync(bookingRequest);

        // Assert
        Assert.False(result.Success);
        Assert.False(result.Data);
        Assert.Equal("Invalid patient information", result.Message);
        Assert.Contains("Patient details are incomplete", result.Errors);
    }
}
