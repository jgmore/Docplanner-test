using System.Net;
using System.Text;
using System.Text.Json;
using Docplanner.Application.Interfaces;
using Docplanner.Common.DTOs;
using Docplanner.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using Xunit;

namespace Docplanner.Tests.Unit;

[Trait("Category", "Unit")]
public class SlotServiceAdapterTests
{
    private static HttpClient CreateMockHttpClient(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        return new HttpClient(handlerMock.Object);
    }

    private static ISlotServiceAdapter CreateAdapter(HttpResponseMessage response)
    {
        var config = Options.Create(new SlotApiOptions
        {
            BaseUrl = "https://mock.api",
            Username = "user",
            Password = "pass"
        });

        return new SlotServiceAdapter(CreateMockHttpClient(response), config);
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_ParsesCorrectly()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F123" },
            SlotDurationMinutes = 30,
            Monday = new DayAvailabilityDto
            {
                WorkPeriod = new WorkPeriodDto { StartHour = 8, LunchStartHour = 12, LunchEndHour = 13, EndHour = 17 },
                BusySlots = []
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        var result = await adapter.FetchWeeklyAvailabilityAsync("20250422");

        Assert.Equal("F123", result.FacilityId);
        Assert.NotEmpty(result.AvailableSlots);
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_Throws_When_StatusCode_NotSuccess()
    {
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Bad request")
        };

        var adapter = CreateAdapter(response);
        var ex = await Assert.ThrowsAsync<ApplicationException>(() => adapter.FetchWeeklyAvailabilityAsync("20250422"));
        Assert.Contains("External Slot Service GetWeeklyAvailability error", ex.Message);
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_Throws_When_Deserialization_Fails()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.FetchWeeklyAvailabilityAsync("20250422"));
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_Throws_When_SlotDuration_IsInvalid()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F123" },
            SlotDurationMinutes = 0 // Invalid duration
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.FetchWeeklyAvailabilityAsync("20250422"));
    }


    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_Throws_When_Facility_IsNull()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = null,
            SlotDurationMinutes = 30
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.FetchWeeklyAvailabilityAsync("20250422"));
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_Throws_When_FacilityId_IsMissing()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "" }, // Missing ID
            SlotDurationMinutes = 30
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        await Assert.ThrowsAsync<InvalidDataException>(() => adapter.FetchWeeklyAvailabilityAsync("20250422"));
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_ReturnsEmpty_When_MondayString_Invalid()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F123" },
            SlotDurationMinutes = 30,
            Monday = new DayAvailabilityDto
            {
                WorkPeriod = new WorkPeriodDto { StartHour = 8, LunchStartHour = 12, LunchEndHour = 13, EndHour = 17 },
                BusySlots = []
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        var result = await adapter.FetchWeeklyAvailabilityAsync("invalid_date");

        Assert.Equal("F123", result.FacilityId);
        Assert.Empty(result.AvailableSlots);
    }


    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_IgnoresBusySlots()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F123" },
            SlotDurationMinutes = 60,
            Monday = new DayAvailabilityDto
            {
                WorkPeriod = new WorkPeriodDto { StartHour = 8, LunchStartHour = 12, LunchEndHour = 13, EndHour = 17 },
                BusySlots = new List<TimeSlotDto>
            {
                new TimeSlotDto
                {
                    Start = DateTime.Parse("2025-04-22T08:00:00"),
                    End = DateTime.Parse("2025-04-22T09:00:00")
                }
            }
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        var result = await adapter.FetchWeeklyAvailabilityAsync("20250422");

        // The slot 08:00-09:00 is busy, should not appear
        Assert.DoesNotContain(result.AvailableSlots, s => s.Start.Hour == 8);
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_Throws_When_DeserializedObjectIsNull()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json") // Explicitly null
        };

        var adapter = CreateAdapter(response);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.FetchWeeklyAvailabilityAsync("20250422"));
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_IgnoresDay_When_WorkPeriod_IsNull()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F123" },
            SlotDurationMinutes = 30,
            Monday = new DayAvailabilityDto
            {
                WorkPeriod = null,
                BusySlots = []
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        var result = await adapter.FetchWeeklyAvailabilityAsync("20250422");

        Assert.Empty(result.AvailableSlots);
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_ReturnsEmpty_When_AllSlotsAreBusy()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F123" },
            SlotDurationMinutes = 60,
            Monday = new DayAvailabilityDto
            {
                WorkPeriod = new WorkPeriodDto
                {
                    StartHour = 8,
                    LunchStartHour = 12,
                    LunchEndHour = 13,
                    EndHour = 17
                },
                BusySlots = new List<TimeSlotDto>
            {
                new TimeSlotDto
                {
                    Start = DateTime.Parse("2025-04-22T08:00:00"),
                    End = DateTime.Parse("2025-04-22T17:00:00")
                }
            }
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        var result = await adapter.FetchWeeklyAvailabilityAsync("20250422");

        Assert.Empty(result.AvailableSlots);
    }


    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_HandlesInvalidDateFormat_ReturnsFacilityWithNoSlots()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F001" },
            SlotDurationMinutes = 30,
            Monday = new DayAvailabilityDto
            {
                WorkPeriod = new WorkPeriodDto { StartHour = 9, LunchStartHour = 12, LunchEndHour = 13, EndHour = 17 },
                BusySlots = []
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        var result = await adapter.FetchWeeklyAvailabilityAsync("22-04-2025"); // Wrong format

        Assert.Equal("F001", result.FacilityId);
        Assert.Empty(result.AvailableSlots);
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_ReturnsNoSlots_When_SlotDurationDoesNotFitInWorkPeriod()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F999" },
            SlotDurationMinutes = 60, // 1-hour slot
            Monday = new DayAvailabilityDto
            {
                WorkPeriod = new WorkPeriodDto
                {
                    StartHour = 8,
                    LunchStartHour = 8,
                    LunchEndHour = 8,
                    EndHour = 8 // Start and end are the same
                },
                BusySlots = []
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        var result = await adapter.FetchWeeklyAvailabilityAsync("20250422");

        Assert.Equal("F999", result.FacilityId);
        Assert.Empty(result.AvailableSlots);
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_ReturnsNoSlots_When_MondayStringIsEmpty()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F888" },
            SlotDurationMinutes = 30,
            Monday = new DayAvailabilityDto
            {
                WorkPeriod = new WorkPeriodDto { StartHour = 8, LunchStartHour = 12, LunchEndHour = 13, EndHour = 17 },
                BusySlots = []
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        var result = await adapter.FetchWeeklyAvailabilityAsync(""); // Empty string

        Assert.Equal("F888", result.FacilityId);
        Assert.Empty(result.AvailableSlots);
    }

    [Fact]
    public async Task FetchWeeklyAvailabilityAsync_SkipsOnlyConflictingSlots_When_PartiallyBusy()
    {
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F777" },
            SlotDurationMinutes = 60,
            Monday = new DayAvailabilityDto
            {
                WorkPeriod = new WorkPeriodDto
                {
                    StartHour = 8,
                    LunchStartHour = 12,
                    LunchEndHour = 13,
                    EndHour = 14
                },
                BusySlots = new List<TimeSlotDto>
            {
                new TimeSlotDto
                {
                    Start = DateTime.Parse("2025-04-22T09:00:00"),
                    End = DateTime.Parse("2025-04-22T10:00:00")
                }
            }
            }
        };

        var json = JsonSerializer.Serialize(dto);
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        var result = await adapter.FetchWeeklyAvailabilityAsync("20250422");

        var slots = result.AvailableSlots.ToList();
        Assert.Contains(slots, s => s.Start.Hour == 8);  // Should exist
        Assert.DoesNotContain(slots, s => s.Start.Hour == 9); // Blocked
        Assert.Contains(slots, s => s.Start.Hour == 10); // Should exist
    }


    [Fact]
    public async Task TakeSlotAsync_ReturnsSuccess()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Success", Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);
        var result = await adapter.TakeSlotAsync(new BookingRequestDto
        {
            FacilityId = "F001",
            Start = "20250422T090000",
            End = "20250422T093000",
            Comments = "Checkup",
            Patient = new PatientDto { Name = "Alice" }
        });

        Assert.True(result.Success);
        Assert.True(result.Data);
        Assert.Equal("Success", result.Message);
    }

    [Fact]
    public async Task TakeSlotAsync_ReturnsError_When_PatientIsNull()
    {
        var adapter = CreateAdapter(new HttpResponseMessage());
        var result = await adapter.TakeSlotAsync(new BookingRequestDto { Patient = null! });

        Assert.False(result.Success);
        Assert.Contains("Patient", result.Errors.First());
    }

    [Fact]
    public async Task TakeSlotAsync_HandlesHttpException()
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Network error"));

        var client = new HttpClient(handlerMock.Object);
        var config = Options.Create(new SlotApiOptions
        {
            BaseUrl = "https://mock.api",
            Username = "user",
            Password = "pass"
        });

        var adapter = new SlotServiceAdapter(client, config);

        var result = await adapter.TakeSlotAsync(new BookingRequestDto
        {
            Patient = new PatientDto { Name = "Alice" }
        });

        Assert.False(result.Success);
        Assert.Contains("Network error", result.Errors.First());
    }

    [Fact]
    public async Task TakeSlotAsync_ReturnsError_When_HttpStatusCodeIsFailure()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("Bad Request", Encoding.UTF8, "application/json")
        };

        var adapter = CreateAdapter(response);

        var bookingRequest = new BookingRequestDto
        {
            FacilityId = "F001",
            Start = "20250422T090000",
            End = "20250422T093000",
            Comments = "Routine Checkup",
            Patient = new PatientDto { Name = "John Doe" }
        };

        // Act
        var result = await adapter.TakeSlotAsync(bookingRequest);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("External Slot Service TakeSlotAsync error:", result.Errors.First());
        Assert.False(result.Data);
    }


    [Fact]
    public void ConvertToAvailableSlots_ReturnsEmpty_When_WeeklyAvailabilityIsNull()
    {
        // Arrange
        var config = Options.Create(new SlotApiOptions
        {
            BaseUrl = "https://mock.api",
            Username = "user",
            Password = "pass"
        });

        var adapter = new SlotServiceAdapter(new HttpClient(), config);

        // Act
        var result = adapter
            .GetType()
            .GetMethod("ConvertToAvailableSlots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(adapter, new object[] { null!, "20250422" }) as IEnumerable<AvailabilitySlotDto>;

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public void ConvertToAvailableSlots_ReturnsEmpty_When_MondayDateFormatIsInvalid()
    {
        // Arrange
        var dto = new WeeklyAvailabilityResponseDto
        {
            Facility = new FacilityDto { FacilityId = "F001" },
            SlotDurationMinutes = 30,
            Monday = new DayAvailabilityDto
            {
                WorkPeriod = new WorkPeriodDto
                {
                    StartHour = 8,
                    LunchStartHour = 12,
                    LunchEndHour = 13,
                    EndHour = 17
                },
                BusySlots = []
            }
        };

        var config = Options.Create(new SlotApiOptions
        {
            BaseUrl = "https://mock.api",
            Username = "user",
            Password = "pass"
        });

        var adapter = new SlotServiceAdapter(new HttpClient(), config);

        var result = adapter
            .GetType()
            .GetMethod("ConvertToAvailableSlots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.Invoke(adapter, new object[] { dto, "invalid_date" }) as IEnumerable<AvailabilitySlotDto>;

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public void ProcessDayAvailability_BusySlotsIsNull_GeneratesExpectedSlots()
    {
        // Arrange
        var client = new HttpClient();
        var options = Options.Create(new SlotApiOptions());
        var adapter = new SlotServiceAdapter(client, options);

        var availableSlots = new List<AvailabilitySlotDto>();
        var workPeriod = new WorkPeriodDto
        {
            StartHour = 9,
            LunchStartHour = 12,
            LunchEndHour = 13,
            EndHour = 17
        };

        var dayAvailability = new DayAvailabilityDto
        {
            WorkPeriod = workPeriod,
            BusySlots = null
        };

        var date = new DateTime(2025, 04, 28); // Monday
        var dayName = "Monday";
        var slotDurationMinutes = 60;

        // Act
        adapter.ProcessDayAvailability(
            dayAvailability,
            date,
            dayName,
            availableSlots,
            slotDurationMinutes
        );

        // Assert
        // Expect 7 available slots: 9-12 (3 slots), 13-17 (4 slots)
        Assert.Equal(7, availableSlots.Count);
        Assert.All(availableSlots, slot => Assert.True(slot.IsAvailable));
        Assert.All(availableSlots, slot => Assert.Equal("Monday", slot.DayOfWeek));
    }


}
