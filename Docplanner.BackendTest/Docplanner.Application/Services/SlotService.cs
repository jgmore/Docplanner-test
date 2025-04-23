using Docplanner.Common.DTOs;
using Docplanner.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;

namespace Docplanner.Application.Services;

public class SlotService : ISlotService
{
    private readonly ISlotServiceAdapter _adapter;
    private readonly ILogger<SlotService> _logger;
    private readonly IMemoryCache _cache;

    public SlotService(ISlotServiceAdapter adapter, ILogger<SlotService> logger, IMemoryCache cache)
    {
        _adapter = adapter;
        _logger = logger;
        _cache = cache;
    }

    public async Task<ApiResponseDto<IEnumerable<AvailabilitySlotDto>>> GetWeeklyAvailabilityAsync(string monday)
    {
        try
        {
            _logger.LogInformation("Fetching availability for week starting {Monday}", monday);
            
            // Cache key
            string cacheKey = $"weekly_availability_{monday}";

            // Try to get from cache
            if (_cache.TryGetValue(cacheKey, out DataResponseDto cachedSlots))
            {
                _logger.LogInformation("Returning cached availability for {Monday}", monday);
                return ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateSuccess(
                    cachedSlots.FacilityId,
                    cachedSlots.AvailableSlots,
                    $"Retrieved {cachedSlots.AvailableSlots.Count()} cached slots for week starting {monday}"
                );
            }


            var slots = await _adapter.FetchWeeklyAvailabilityAsync(monday);

            // Save to cache for 5 minutes
            _cache.Set(cacheKey, slots, TimeSpan.FromMinutes(5));

            _logger.LogInformation("Retrieved {Count} slots", slots.AvailableSlots.Count());

            return ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateSuccess(
                slots.FacilityId,
                slots.AvailableSlots,
                $"Retrieved {slots.AvailableSlots.Count()} slots for week starting {monday}"
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

    private string CalculateMonday(string input)
    {
        DateTime date = DateTime.ParseExact(input, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        // Calculate Monday (start of the week)
        DateTime monday = date.AddDays(-(int)date.DayOfWeek + (date.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));

        // Format it back to the desired "yyyyMMdd" format if needed
        string mondayString = monday.ToString("yyyyMMdd");
        return mondayString;
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

            string computedMondayDate = CalculateMonday(request.Start);
            _cache.Remove($"weekly_availability_{computedMondayDate}");

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