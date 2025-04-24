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
        Assert.Contains("Slot Service error", ex.Message);
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
}
