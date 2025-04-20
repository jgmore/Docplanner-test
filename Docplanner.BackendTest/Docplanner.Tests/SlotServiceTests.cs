using Docplanner.Application.Interfaces;
using Docplanner.Application.Services;
using Docplanner.Common.DTOs;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Globalization;

namespace Docplanner.Tests;

public class SlotServiceTests
{
    [Fact]
    public async Task GetWeeklyAvailability_ShouldReturnSlots()
    {
        // Arrange
        var adapterMock = new Mock<ISlotServiceAdapter>();
        var loggerMock = new Mock<ILogger<SlotService>>();

        var mondayDate = DateTime.ParseExact("20250415", "yyyyMMdd", CultureInfo.InvariantCulture);

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

        adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync("20250415"))
            .ReturnsAsync(mockSlots);

        var service = new SlotService(adapterMock.Object, loggerMock.Object);

        // Act
        var result = await service.GetWeeklyAvailabilityAsync("20250415");

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Data);
        Assert.Contains(result.Data, slot => slot.Start == mondayDate.AddHours(10));
    }

    [Fact]
    public async Task BookSlot_ShouldReturnSuccess_WhenBookingSucceeds()
    {
        // Arrange
        var adapterMock = new Mock<ISlotServiceAdapter>();
        var loggerMock = new Mock<ILogger<SlotService>>();

        adapterMock.Setup(a => a.TakeSlotAsync(It.IsAny<BookingRequestDto>()))
            .ReturnsAsync(ApiResponseDto<bool>.CreateSuccess(true, "Booked successfully"));

        var service = new SlotService(adapterMock.Object, loggerMock.Object);

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
        // Arrange
        var adapterMock = new Mock<ISlotServiceAdapter>();
        var loggerMock = new Mock<ILogger<SlotService>>();

        adapterMock.Setup(a => a.TakeSlotAsync(It.IsAny<BookingRequestDto>()))
            .ReturnsAsync(ApiResponseDto<bool>.CreateError(
                "Invalid patient information",
                new[] { "Patient details are incomplete" }));

        var service = new SlotService(adapterMock.Object, loggerMock.Object);

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
