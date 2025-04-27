using Docplanner.Common.DTOs;
using Docplanner.Application.Interfaces;
using System.Globalization;

namespace Docplanner.Infrastructure.Adapters;

public class MockSlotServiceAdapter : ISlotServiceAdapter
{
    public async Task<DataResponseDto> FetchWeeklyAvailabilityAsync(string monday)
    {
        // Simulate network/API latency
        await Task.Delay(100);

        if (!DateTime.TryParseExact(monday,
                                    "yyyyMMdd",
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.None,
                                    out var mondayDate))
        {
            throw new ArgumentException("Invalid date format for 'monday'", nameof(monday));
        }
        DataResponseDto dataResponseDto = new DataResponseDto();

        dataResponseDto.AvailableSlots = new List<AvailabilitySlotDto>
        {
            new AvailabilitySlotDto {
                Start = mondayDate.AddHours(10),
                End = mondayDate.AddHours(10).AddMinutes(20),
                DayOfWeek = "Monday",
                IsAvailable = true
            },
            new AvailabilitySlotDto {
                Start = mondayDate.AddHours(10).AddMinutes(30),
                End = mondayDate.AddHours(10).AddMinutes(50),
                DayOfWeek = "Monday",
                IsAvailable = true
            }
        };
        dataResponseDto.FacilityId = "Id1";
        return dataResponseDto;
    }

    public async Task<ApiResponseDto<bool>> TakeSlotAsync(BookingRequestDto request)
    {
        // Simulate network/API latency
        await Task.Delay(100);

        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), "Booking request cannot be null");
        }

        if (string.IsNullOrEmpty(request.Start) || string.IsNullOrEmpty(request.End))
        {
            return ApiResponseDto<bool>.CreateError(
                "Invalid slot times",
                new[] { "Start and End times are required" }
            );
        }

        if (request.Patient == null)
        {
            return ApiResponseDto<bool>.CreateError(
                "Patient information is required",
                new[] { "Patient details are missing" }
            );
        }

        // Validation of Patient data
        if (string.IsNullOrEmpty(request.Patient.Email) ||
            string.IsNullOrEmpty(request.Patient.Name) ||
            string.IsNullOrEmpty(request.Patient.SecondName))
        {
            return ApiResponseDto<bool>.CreateError(
                "Invalid patient information",
                new[] { "Patient name, second name and email are required" }
            );
        }

        return ApiResponseDto<bool>.CreateSuccess(
            "Id1",
            true,
            "Mock booking confirmed"
        );
    }
}
