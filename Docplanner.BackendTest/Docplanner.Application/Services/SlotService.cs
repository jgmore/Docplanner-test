using Docplanner.Common.DTOs;
using Docplanner.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Docplanner.Application.Services;

public class SlotService : ISlotService
{
    private readonly ISlotServiceAdapter _adapter;
    private readonly ILogger<SlotService> _logger;

    public SlotService(ISlotServiceAdapter adapter, ILogger<SlotService> logger)
    {
        _adapter = adapter;
        _logger = logger;
    }

    public async Task<ApiResponseDto<IEnumerable<AvailabilitySlotDto>>> GetWeeklyAvailabilityAsync(string monday)
    {
        try
        {
            _logger.LogInformation("Fetching availability for week starting {Monday}", monday);
            var slots = await _adapter.FetchWeeklyAvailabilityAsync(monday);
            _logger.LogInformation("Retrieved {Count} slots", slots.Count());

            return ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateSuccess(
                slots,
                $"Retrieved {slots.Count()} slots for week starting {monday}"
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching availability for week starting {Monday}", monday);
            return ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateError(
                "Error retrieving availability",
                new[] { ex.Message }
            );
        }
    }

    public async Task<ApiResponseDto<bool>> BookSlotAsync(BookingRequestDto request)
    {
        try
        {
            _logger.LogInformation("Booking slot from {Start} to {End} for {Email}",
                request.Start, request.End, request.Patient.Email);

            var result = await _adapter.TakeSlotAsync(request);
            _logger.LogInformation("Booking result: {Success} - {Message}",
                result.Success, result.Message);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error booking slot");
            return ApiResponseDto<bool>.CreateError(
                "Error processing booking",
                new[] { ex.Message }
            );
        }
    }
}