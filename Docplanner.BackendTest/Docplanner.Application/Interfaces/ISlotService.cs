using Docplanner.Common.DTOs;

namespace Docplanner.Application.Interfaces;

public interface ISlotService
{
    Task<ApiResponseDto<IEnumerable<AvailabilitySlotDto>>> GetWeeklyAvailabilityAsync(string monday);

    Task<ApiResponseDto<bool>> BookSlotAsync(BookingRequestDto request);
}