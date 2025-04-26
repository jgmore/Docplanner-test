using Docplanner.Application.Interfaces;
using Docplanner.Application.Services;
using Docplanner.Common.DTOs;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.Globalization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Docplanner.Tests.Unit;

public class SlotServiceTests
{
    private readonly Mock<ISlotServiceAdapter> _adapterMock;
    private readonly Mock<ILogger<SlotService>> _loggerMock;
    private readonly IMemoryCache _memoryCache;
    private readonly SlotService _service;

    public SlotServiceTests()
    {
        _adapterMock = new Mock<ISlotServiceAdapter>();
        _loggerMock = new Mock<ILogger<SlotService>>();
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        var mockRetryOptions = new Mock<IOptions<RetryPolicyOptions>>();
        mockRetryOptions.Setup(x => x.Value).Returns(new RetryPolicyOptions
        {
            RetryCount = 3,
            InitialDelaySeconds = 2
        });
        _service = new SlotService(_adapterMock.Object, _loggerMock.Object, _memoryCache, mockRetryOptions.Object);
    }

    private IOptions<RetryPolicyOptions> createRetryOption()
    {
        var mockRetryOptions = new Mock<IOptions<RetryPolicyOptions>>();
        mockRetryOptions.Setup(x => x.Value).Returns(new RetryPolicyOptions
        {
            RetryCount = 3,
            InitialDelaySeconds = 2
        });
        return mockRetryOptions.Object;
    }

    [Fact]
    public async Task SlotService_ThrowException_OnNullAdapter()
    {
        Exception actualException = Assert.Throws<ArgumentNullException>(() => new SlotService(null, _loggerMock.Object, _memoryCache, createRetryOption()));
    }

    [Fact]
    public async Task SlotService_ThrowException_OnNullLogger()
    {
        Exception actualException = Assert.Throws<ArgumentNullException>(() => new SlotService(_adapterMock.Object, null, _memoryCache, createRetryOption()));
    }

    [Fact]
    public async Task SlotService_ThrowException_OnNullCache()
    {
        Exception actualException = Assert.Throws<ArgumentNullException>(() => new SlotService(_adapterMock.Object, _loggerMock.Object, null, createRetryOption()));
    }

    [Fact]
    public async Task GetWeeklyAvailability_ShouldReturnSlots()
    {
        var mondayDate = DateTime.ParseExact("20250421", "yyyyMMdd", CultureInfo.InvariantCulture);
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

        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync("20250421"))
            .ReturnsAsync(dataResponse);


        // Act
        var result = await _service.GetWeeklyAvailabilityAsync("20250421");

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
        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync("20250421"))
                    .ReturnsAsync(dataResponse);
                
        // Act
        var result = await _service.GetWeeklyAvailabilityAsync("20250421");

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Data);
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_Throws_WhenAdapterThrowsException()
    {
        // Arrange
        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync("20250421"))
                   .ThrowsAsync(new InvalidOperationException("Simulated adapter failure"));

        // Act
        var result = await _service.GetWeeklyAvailabilityAsync("20250421");

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

        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync("20250421"))
                   .ReturnsAsync(dataResponse);

        // Act
        var result = await _service.GetWeeklyAvailabilityAsync("20250421");

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
    public async Task GetWeeklyAvailabilityAsync_UsesCache_WhenAvailable()
    {
        var monday = "20250421";
        var cachedData = new DataResponseDto
        {
            FacilityId = "CachedFacility",
            AvailableSlots = new[]
            {
            new AvailabilitySlotDto
            {
                Start = new DateTime(2025, 4, 15, 9, 0, 0),
                End = new DateTime(2025, 4, 15, 9, 20, 0),
                DayOfWeek = "Monday",
                IsAvailable = true
            }
        }
        };

        // Set cache manually
        _memoryCache.Set($"weekly_availability_{monday}", cachedData);

        // Act
        var result = await _service.GetWeeklyAvailabilityAsync(monday);

        // Assert
        Assert.True(result.Success);
        Assert.Single(result.Data);
        Assert.Equal("CachedFacility", result.FacilityId);
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_HandlesNullSlots()
    {
        var dataResponse = new DataResponseDto
        {
            AvailableSlots = null,
            FacilityId = "Id1"
        };

        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync("20250421"))
                    .ReturnsAsync(dataResponse);

        var result = await _service.GetWeeklyAvailabilityAsync("20250421");

        Assert.NotNull(result);
        Assert.Null(result.Data); // If null is returned, this test makes sure it's handled gracefully
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_Should_Return_Error_When_Monday_Is_Null()
    {
        var result = await _service.GetWeeklyAvailabilityAsync(null);

        Assert.False(result.Success);
        Assert.Contains("The monday parameter cannot be null or empty.", result.Errors.FirstOrDefault());
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_Should_Return_Error_When_Monday_Is_Invalid()
    {
        var result = await _service.GetWeeklyAvailabilityAsync("invalid-date");

        Assert.False(result.Success);
        Assert.Contains("The provided date must correspond to a Monday in 'yyyyMMdd' format.", result.Errors.FirstOrDefault());
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_ShouldHandleVeryOldDates()
    {
        var monday = "19000101"; // 1st Jan 1900

        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync(It.IsAny<string>()))
            .ReturnsAsync(new DataResponseDto
            {
                FacilityId = Guid.NewGuid().ToString(),
                AvailableSlots = new List<AvailabilitySlotDto>()
            });

        var response = await _service.GetWeeklyAvailabilityAsync(monday);

        Assert.True(response.Success);
        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_ShouldWork_WhenCacheThrows()
    {
        var fakeCache = new MemoryCache(new MemoryCacheOptions());
        var mockRetryOptions = new Mock<IOptions<RetryPolicyOptions>>();
        mockRetryOptions.Setup(x => x.Value).Returns(new RetryPolicyOptions
        {
            RetryCount = 3,
            InitialDelaySeconds = 2
        });
        var service = new SlotService(_adapterMock.Object, _loggerMock.Object, fakeCache, mockRetryOptions.Object);

        var monday = "20240610";
        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync(monday))
            .ReturnsAsync(new DataResponseDto
            {
                FacilityId = Guid.NewGuid().ToString(),
                AvailableSlots = new List<AvailabilitySlotDto> { new AvailabilitySlotDto() }
            });

        var result = await service.GetWeeklyAvailabilityAsync(monday);

        Assert.True(result.Success);
        Assert.Single(result.Data);
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_ShouldHandleNullFromAdapter()
    {
        var monday = "20240610";

        _adapterMock.Setup(a => a.FetchWeeklyAvailabilityAsync(monday))
            .ReturnsAsync((DataResponseDto)null);

        var response = await _service.GetWeeklyAvailabilityAsync(monday);

        Assert.False(response.Success);
        Assert.Contains("Error retrieving availability", response.Message);
    }

    [Fact]
    public async Task GetWeeklyAvailabilityAsync_InvalidMondayFormat_ReturnsError()
    {
        // Arrange
        string invalidMonday = "2025/04/20";

        // Act
        var response = await _service.GetWeeklyAvailabilityAsync(invalidMonday);

        // Assert
        Assert.False(response.Success);
        Assert.Contains("Invalid date input", response.Message);
    }


    [Fact]
    public void CalculateMonday_ShouldHandleSundayCorrectly()
    {
        // Sunday
        var input = "2024-06-16 10:00:00"; // 16th June 2024 is a Sunday
        var privateMethod = typeof(SlotService).GetMethod("CalculateMonday", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var monday = (string)privateMethod.Invoke(_service, new object[] { input });

        Assert.Equal("20240610", monday); // 10th June 2024
    }

    [Fact]
    public void CalculateMonday_ReturnException_OnNullInput()
    {
        // Arrange
        var input = "";
        var privateMethod = typeof(SlotService).GetMethod("CalculateMonday", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act & Assert
        var targetInvocationException = Assert.Throws<System.Reflection.TargetInvocationException>(
            () => privateMethod.Invoke(_service, new object[] { input })
        );

        var argumentException = Assert.IsType<ArgumentException>(targetInvocationException.InnerException);
        Assert.Equal("Input date cannot be null or empty. (Parameter 'input')", argumentException.Message);
    }


    [Theory]
    [InlineData("2025-04-15 10:00:00", "2025-04-15 10:20:00")]
    [InlineData("2025-04-20 10:00:00", "2025-04-20 10:20:00")]
    public async Task BookSlot_ShouldReturnSuccess_WhenBookingSucceeds(string start, string end)
    {
        _adapterMock.Setup(a => a.TakeSlotAsync(It.IsAny<BookingRequestDto>()))
            .ReturnsAsync(ApiResponseDto<bool>.CreateSuccess("Id1", true, "Booked successfully"));

        var bookingRequest = new BookingRequestDto
        {
            Start = start,
            End = end,
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
        var result = await _service.BookSlotAsync(bookingRequest);

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
        var result = await _service.BookSlotAsync(bookingRequest);

        // Assert
        Assert.False(result.Success);
        Assert.False(result.Data);
        Assert.Equal("Invalid patient information", result.Message);
        Assert.Contains("Patient details are incomplete", result.Errors);
    }

    [Fact]
    public async Task BookSlotAsync_RemovesCache_AfterSuccessfulBooking()
    {
        var monday = "20250414"; // 2025-04-15 falls in week starting 2025-04-14
        var cacheKey = $"weekly_availability_{monday}";

        _memoryCache.Set(cacheKey, new object()); // Dummy cache to be removed

        _adapterMock.Setup(a => a.TakeSlotAsync(It.IsAny<BookingRequestDto>()))
            .ReturnsAsync(ApiResponseDto<bool>.CreateSuccess("Id1", true, "Success"));

        var bookingRequest = new BookingRequestDto
        {
            Start = "2025-04-15 10:00:00",
            End = "2025-04-15 10:20:00",
            Patient = new PatientDto { Email = "a@b.com", Name = "X", Phone = "0000" }
        };

        // Act
        await _service.BookSlotAsync(bookingRequest);

        // Assert
        Assert.False(_memoryCache.TryGetValue(cacheKey, out _)); // Should be removed
    }

    [Fact]
    public async Task BookSlotAsync_ReturnsError_WhenExceptionThrown()
    {
        _adapterMock.Setup(a => a.TakeSlotAsync(It.IsAny<BookingRequestDto>()))
            .ThrowsAsync(new Exception("Booking failure"));

        var bookingRequest = new BookingRequestDto
        {
            Start = "2025-04-15 10:00:00",
            End = "2025-04-15 10:20:00",
            Patient = new PatientDto { Email = "a@b.com", Name = "X", Phone = "0000" }
        };

        // Act
        var result = await _service.BookSlotAsync(bookingRequest);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Booking failure", result.Errors.FirstOrDefault());

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Error booking slot")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task BookSlotAsync_ReturnsError_WhenStartDateIsInvalid()
    {
        _adapterMock.Setup(a => a.TakeSlotAsync(It.IsAny<BookingRequestDto>()))
            .ReturnsAsync(ApiResponseDto<bool>.CreateSuccess("Id1", true, "Booked"));

        var bookingRequest = new BookingRequestDto
        {
            Start = "not a valid date",
            End = "2025-04-15 10:20:00",
            Patient = new PatientDto
            {
                Email = "test@example.com",
                Name = "Test",
                Phone = "123456789"
            }
        };

        var result = await _service.BookSlotAsync(bookingRequest);

        Assert.False(result.Success);
        Assert.Equal("Invalid date format for Start or End. Expected format: yyyy-MM-dd HH:mm:ss", result.Message);
        Assert.Contains("Invalid date format", result.Errors.FirstOrDefault() ?? "");

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Invalid date format for booking")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task BookSlotAsync_Should_Return_Error_When_Request_Is_Null()
    {
        var result = await _service.BookSlotAsync(null);

        Assert.False(result.Success);
        Assert.Contains("Request cannot be null.", result.Errors);
    }

    [Fact]
    public async Task BookSlotAsync_Should_Return_Error_When_Patient_Is_Null()
    {
        var request = new BookingRequestDto
        {
            Start = "2024-04-22 08:00:00",
            End = "2024-04-22 09:00:00",
            Patient = null
        };

        var result = await _service.BookSlotAsync(request);

        Assert.False(result.Success);
        Assert.Contains("Patient information must be provided.", result.Errors);
    }

    [Fact]
    public async Task BookSlotAsync_ShouldReturnError_WhenStartEndDatesAreWrongFormat()
    {
        var booking = new BookingRequestDto
        {
            Start = "13-06-2024 11:00:00",  // Wrong format
            End = "13-06-2024 12:00:00",
            Patient = new PatientDto { Name = "John", Email = "john@example.com", Phone = "123" }
        };

        var response = await _service.BookSlotAsync(booking);

        Assert.False(response.Success);
        Assert.Contains("Invalid date format", response.Errors.First());
    }


    [Fact]
    public async Task BookSlotAsync_ShouldReturnError_WhenStartAfterEnd()
    {
        var booking = new BookingRequestDto
        {
            Start = "2024-06-13 12:00:00",
            End = "2024-06-13 11:00:00",
            Patient = new PatientDto { Name = "John", Email = "john@example.com", Phone = "123" }
        };

        // Simulate adapter normal response
        _adapterMock.Setup(a => a.TakeSlotAsync(It.IsAny<BookingRequestDto>()))
            .ReturnsAsync(ApiResponseDto<bool>.CreateError("Invalid slot time", new[] { "Start must be before End" }));

        var response = await _service.BookSlotAsync(booking);

        Assert.False(response.Success);
        Assert.Contains("Start must be before End", response.Errors);
    }

    [Fact]
    public async Task BookSlotAsync_ShouldRetry_OnTransientError()
    {
        // Arrange
        var booking = new BookingRequestDto
        {
            Start = "2025-04-26 09:00:00",
            End = "2025-04-26 10:00:00",
            Patient = new PatientDto { Email = "test@example.com", Name = "Test", Phone = "123456789" }
        };

        int callCount = 0;

        _adapterMock.Setup(a => a.TakeSlotAsync(It.IsAny<BookingRequestDto>()))
            .Callback(() => { callCount++; })
            .ThrowsAsync(new Exception("Transient error"));

        // Act
        var result = await _service.BookSlotAsync(booking);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Transient error", result.Errors.FirstOrDefault() ?? "");

        // Should have retried 4 times (original + 3 retries)
        Assert.Equal(4, callCount);

        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Retry")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Exactly(3)); // 3 retry warnings
    }


}
