using Docplanner.Application.Interfaces;
using Docplanner.Common.DTOs;
using Docplanner.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

public class SlotServiceAdapter(HttpClient client, IOptions<SlotApiOptions> options) : ISlotServiceAdapter
{
    private readonly SlotApiOptions _config = options.Value;

    public async Task<DataResponseDto> FetchWeeklyAvailabilityAsync(string monday)
    {
        AddAuthHeader();
        var response = await client.GetAsync($"{_config.BaseUrl}/GetWeeklyAvailability/{monday}");
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new ApplicationException($"Slot Service error: {(int)response.StatusCode} - {error}");
        }


        var json = await response.Content.ReadAsStringAsync();
        var weeklyAvailability = JsonSerializer.Deserialize<WeeklyAvailabilityResponseDto>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (weeklyAvailability == null)
        {
            throw new InvalidOperationException("Deserialization of WeeklyAvailabilityResponseDto returned null.");
        }

        if (weeklyAvailability.Facility == null || string.IsNullOrWhiteSpace(weeklyAvailability.Facility.FacilityId))
        {
            throw new InvalidDataException("Facility data is missing or incomplete in the Slot API response.");
        }

        if (weeklyAvailability.SlotDurationMinutes <= 0)
        {
            throw new InvalidDataException("Slot duration is invalid or not provided.");
        }

        var availableSlots = ConvertToAvailableSlots(weeklyAvailability, monday);

        return new DataResponseDto
        {
            AvailableSlots = availableSlots,
            FacilityId = weeklyAvailability.Facility.FacilityId
        };
    }

    private IEnumerable<AvailabilitySlotDto> ConvertToAvailableSlots(
        WeeklyAvailabilityResponseDto? weeklyAvailability, string mondayString)
    {
        if (weeklyAvailability == null)
            return Enumerable.Empty<AvailabilitySlotDto>();

        var availableSlots = new List<AvailabilitySlotDto>();

        // Parseamos la fecha del lunes para tener una referencia
        if (!DateTime.TryParseExact(mondayString, "yyyyMMdd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var mondayDate))
        {
            return Enumerable.Empty<AvailabilitySlotDto>();
        }

        // Procesamos cada día de la semana
        ProcessDayAvailability(weeklyAvailability.Monday, mondayDate.AddDays(0), "Monday", availableSlots, weeklyAvailability.SlotDurationMinutes);
        ProcessDayAvailability(weeklyAvailability.Tuesday, mondayDate.AddDays(1), "Tuesday", availableSlots, weeklyAvailability.SlotDurationMinutes);
        ProcessDayAvailability(weeklyAvailability.Wednesday, mondayDate.AddDays(2), "Wednesday", availableSlots, weeklyAvailability.SlotDurationMinutes);
        ProcessDayAvailability(weeklyAvailability.Thursday, mondayDate.AddDays(3), "Thursday", availableSlots, weeklyAvailability.SlotDurationMinutes);
        ProcessDayAvailability(weeklyAvailability.Friday, mondayDate.AddDays(4), "Friday", availableSlots, weeklyAvailability.SlotDurationMinutes);
        ProcessDayAvailability(weeklyAvailability.Saturday, mondayDate.AddDays(5), "Saturday", availableSlots, weeklyAvailability.SlotDurationMinutes);
        ProcessDayAvailability(weeklyAvailability.Sunday, mondayDate.AddDays(6), "Sunday", availableSlots, weeklyAvailability.SlotDurationMinutes);

        return availableSlots;
    }

    private void ProcessDayAvailability(
        DayAvailabilityDto? dayAvailability,
        DateTime date,
        string dayName,
        List<AvailabilitySlotDto> availableSlots,
        int slotDurationMinutes)
    {
        if (dayAvailability?.WorkPeriod == null)
            return;

        var workPeriod = dayAvailability.WorkPeriod;
        var busySlots = dayAvailability.BusySlots ?? new List<TimeSlotDto>();

        // Generamos los slots para el período de mañana y tarde
        GenerateAvailableSlots(
            date.AddHours(workPeriod.StartHour),
            date.AddHours(workPeriod.LunchStartHour),
            slotDurationMinutes,
            busySlots,
            dayName,
            availableSlots);

        GenerateAvailableSlots(
            date.AddHours(workPeriod.LunchEndHour),
            date.AddHours(workPeriod.EndHour),
            slotDurationMinutes,
            busySlots,
            dayName,
            availableSlots);
    }

    private void GenerateAvailableSlots(
        DateTime start,
        DateTime end,
        int slotDurationMinutes,
        List<TimeSlotDto> busySlots,
        string dayName,
        List<AvailabilitySlotDto> availableSlots)
    {
        var currentSlotStart = start;

        while (currentSlotStart.AddMinutes(slotDurationMinutes) <= end)
        {
            var currentSlotEnd = currentSlotStart.AddMinutes(slotDurationMinutes);

            // Verificamos si el slot está ocupado
            bool isSlotBusy = busySlots.Any(busy =>
                (busy.Start <= currentSlotStart && busy.End > currentSlotStart) ||
                (busy.Start < currentSlotEnd && busy.End >= currentSlotEnd) ||
                (busy.Start >= currentSlotStart && busy.End <= currentSlotEnd));

            if (!isSlotBusy)
            {
                availableSlots.Add(new AvailabilitySlotDto
                {
                    Start = currentSlotStart,
                    End = currentSlotEnd,
                    DayOfWeek = dayName,
                    IsAvailable = true
                });
            }

            currentSlotStart = currentSlotEnd;
        }
    }

    public async Task<ApiResponseDto<bool>> TakeSlotAsync(BookingRequestDto request)
    {
        AddAuthHeader();

        // Validamos la estructura del request
        if (request.Patient == null)
        {
            return ApiResponseDto<bool>.CreateError(
                "Patient information is required",
                new[] { "The Patient object cannot be null" });
        }

        var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync($"{_config.BaseUrl}/TakeSlot", content);
            var body = await response.Content.ReadAsStringAsync();

            return new ApiResponseDto<bool>
            {
                Success = response.IsSuccessStatusCode,
                Message = body,
                Data = response.IsSuccessStatusCode
            };
        }
        catch (Exception ex)
        {
            return ApiResponseDto<bool>.CreateError(
                "Error taking slot",
                new[] { ex.Message }
            );
        }
    }

    private void AddAuthHeader()
    {
        var authValue = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.Username}:{_config.Password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", authValue);
    }
}
