using Docplanner.Common.DTOs;

namespace Docplanner.Application.Interfaces;

public interface ISlotServiceAdapter
{
    Task<IEnumerable<AvailabilitySlotDto>> FetchWeeklyAvailabilityAsync(string monday);

    Task<ApiResponseDto<bool>> TakeSlotAsync(BookingRequestDto request);
}