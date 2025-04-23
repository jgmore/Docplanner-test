using Xunit;
using Docplanner.Application.Services;
using Docplanner.Infrastructure.Adapters;
using Docplanner.Common.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace Docplanner.Tests.Integration;

public class SlotServiceTests_Integration
{
    private readonly SlotService _service;

    public SlotServiceTests_Integration()
    {
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var adapter = new MockSlotServiceAdapter();
        _service = new SlotService(adapter, Microsoft.Extensions.Logging.Abstractions.NullLogger<SlotService>.Instance, memoryCache);
    }

    [Fact]
    public async Task CanFetchAndBookSlot()
    {
        var response = await _service.GetWeeklyAvailabilityAsync("20250415");
        Assert.NotEmpty(response.Data);

        AvailabilitySlotDto[] slots = response.Data.ToArray();
        var request = new BookingRequestDto
        {
            FacilityId = response.FacilityId,
            Start = slots[0].Start.ToString("yyyy-MM-dd HH:mm:ss"),
            End = slots[0].End.ToString("yyyy-MM-dd HH:mm:ss"),
            Patient = new PatientDto
            {
                Name = "Test",
                SecondName = "User",
                Email = "integration@test.com",
                Phone = "123456789"
            }
        };

        var result = await _service.BookSlotAsync(request);
        Assert.True(result.Success);
    }
}