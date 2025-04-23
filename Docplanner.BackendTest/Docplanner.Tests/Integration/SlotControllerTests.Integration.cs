using Xunit;
using Microsoft.AspNetCore.Mvc;
using Docplanner.API.Controllers;
using Docplanner.Application.Services;
using Docplanner.Infrastructure.Adapters;
using Docplanner.Common.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace Docplanner.Tests.Integration;
public class SlotControllerTests_Integration
{
    private readonly SlotsController _controller;

    public SlotControllerTests_Integration()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var service = new SlotService(new MockSlotServiceAdapter(), Microsoft.Extensions.Logging.Abstractions.NullLogger<SlotService>.Instance, memoryCache);
        _controller = new SlotsController(service);
    }

    [Fact]
    public async Task BookAndGetSlots_RoundTrip()
    {
        var availabilityResult = await _controller.GetWeeklyAvailability("20250415");
        var okResult = Assert.IsType<OkObjectResult>(availabilityResult);
        var response = Assert.IsAssignableFrom<ApiResponseDto<IEnumerable<AvailabilitySlotDto>>>(okResult.Value);
        Assert.NotEmpty(response.Data);

        AvailabilitySlotDto[] slots = response.Data.ToArray();
        var bookingRequest = new BookingRequestDto
        {
            FacilityId = response.FacilityId,
            Start = slots[0].Start.ToString("yyyy-MM-dd HH:mm:ss"),
            End = slots[0].End.ToString("yyyy-MM-dd HH:mm:ss"),
            Patient = new PatientDto
            {
                Name = "Test",
                SecondName = "User",
                Email = "e2e@integration.com",
                Phone = "123456789"
            }
        };

        var bookingResult = await _controller.BookSlot(bookingRequest);
        Assert.IsType<OkObjectResult>(bookingResult);
    }
}