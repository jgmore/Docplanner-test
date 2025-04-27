using Docplanner.Common.DTOs;
using Docplanner.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using Polly.Retry;
using Polly;
using Microsoft.Extensions.Options;

namespace Docplanner.Application.Services;

public class SlotService : ISlotService
{
    private readonly ISlotServiceAdapter _adapter;
    private readonly ILogger<SlotService> _logger;
    private readonly IMemoryCache _cache;
    private readonly AsyncRetryPolicy _retryPolicy;

    public SlotService(ISlotServiceAdapter adapter, ILogger<SlotService> logger, IMemoryCache cache, IOptions<RetryPolicyOptions> retryOptions)
    {
        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));

        var options = retryOptions.Value;
        _retryPolicy = Policy
        .Handle<Exception>()
        .WaitAndRetryAsync(
            retryCount: options.RetryCount,
            sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(options.InitialDelaySeconds, attempt)),
            onRetry: (exception, timespan, retryCount, context) =>
            {
                _logger.LogWarning(exception, "Retry {RetryAttempt} after {Delay}s due to error: {ErrorMessage}",
                    retryCount, timespan.TotalSeconds, exception.Message);
            });

    }

    public async Task<ApiResponseDto<IEnumerable<AvailabilitySlotDto>>> GetWeeklyAvailabilityAsync(string monday)
    {
        if (string.IsNullOrWhiteSpace(monday))
        {
            _logger.LogWarning("Provided monday parameter is null or empty.");
            return ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateError(
                "Invalid date input",
                new[] { "The monday parameter cannot be null or empty." }
            );
        }

        try
        {
            _logger.LogInformation("Fetching availability for week starting {Monday}", monday);

            if (!IsValidMonday(monday))
            {
                _logger.LogWarning("Provided date {Monday} is not a valid Monday format.", monday);
                return ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateError(
                    "Invalid date input",
                    new[] { "The provided date must correspond to a Monday in 'yyyyMMdd' format." }
                );
            }

            string cacheKey = $"weekly_availability_{monday}";

            if (_cache.TryGetValue(cacheKey, out DataResponseDto cachedSlots))
            {
                _logger.LogInformation("Returning cached availability for {Monday}", monday);
                return ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateSuccess(
                    cachedSlots.FacilityId,
                    cachedSlots.AvailableSlots,
                    $"Retrieved {cachedSlots.AvailableSlots.Count()} cached slots for week starting {monday}"
                );
            }

            var slots = await _retryPolicy.ExecuteAsync(() => _adapter.FetchWeeklyAvailabilityAsync(monday));

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
            string logMessage = "Error fetching availability for week starting {Monday}";
            string responseMessage = "Error retrieving availability";
            if (ex is ApplicationException)
            {
                logMessage += " on external Slot Service";
                responseMessage = " on external Slot Service";
            }
            _logger.LogError(ex, logMessage , monday);
            return ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateError(
                responseMessage,
                new[] { ex.Message }
            );
        }
    }

    public async Task<ApiResponseDto<bool>> BookSlotAsync(BookingRequestDto request)
    {
        if (request == null)
        {
            _logger.LogWarning("Booking request is null.");
            return ApiResponseDto<bool>.CreateError(
                "Invalid booking request",
                new[] { "Request cannot be null." }
            );
        }

        if (!DateTime.TryParseExact(request.Start, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ||
            !DateTime.TryParseExact(request.End, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            _logger.LogWarning("Invalid date format for booking: {Start} to {End}", request.Start, request.End);
            return ApiResponseDto<bool>.CreateError(
                "Invalid date format for Start or End. Expected format: yyyy-MM-dd HH:mm:ss",
                new[] { $"Invalid date format: Start='{request.Start}', End='{request.End}'" }
            );
        }

        if (request.Patient == null)
        {
            _logger.LogWarning("Booking request patient is null.");
            return ApiResponseDto<bool>.CreateError(
                "Invalid booking request",
                new[] { "Patient information must be provided." }
            );
        }

        try
        {
            _logger.LogInformation("Booking slot from {Start} to {End} for {Email}",
                request.Start, request.End, request.Patient.Email);

            var result = await _retryPolicy.ExecuteAsync(() => _adapter.TakeSlotAsync(request));

            _logger.LogInformation("Booking result: {Success} - {Message}",
                result.Success, result.Message);

            string computedMondayDate = CalculateMonday(request.Start);

            _cache.Remove($"weekly_availability_{computedMondayDate}");

            return result;
        }
        catch (Exception ex)
        {
            string logMessage = "Error booking slot";
            string responseMessage = "Error processing booking";
            if (ex is ApplicationException)
            {
                logMessage += " on external Slot Service";
                responseMessage = " on external Slot Service";
            }
            _logger.LogError(ex, logMessage);
            return ApiResponseDto<bool>.CreateError(
                responseMessage,
                new[] { ex.Message }
            );
        }
    }

    private string CalculateMonday(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input date cannot be null or empty.", nameof(input));

        DateTime date = DateTime.ParseExact(input, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        DateTime monday = date.AddDays(-(int)date.DayOfWeek + (date.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));

        return monday.ToString("yyyyMMdd");
    }

    private bool IsValidMonday(string input)
    {
        if (input.Length != 8 || !DateTime.TryParseExact(input, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return false;
        }

        return date.DayOfWeek == DayOfWeek.Monday;
    }
}
