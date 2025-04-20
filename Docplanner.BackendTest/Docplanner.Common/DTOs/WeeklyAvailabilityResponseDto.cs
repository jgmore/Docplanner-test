namespace Docplanner.Common.DTOs;

public class WeeklyAvailabilityResponseDto
{
    public FacilityDto? Facility { get; set; }
    public int SlotDurationMinutes { get; set; }
    public DayAvailabilityDto? Monday { get; set; }
    public DayAvailabilityDto? Tuesday { get; set; }
    public DayAvailabilityDto? Wednesday { get; set; }
    public DayAvailabilityDto? Thursday { get; set; }
    public DayAvailabilityDto? Friday { get; set; }
    public DayAvailabilityDto? Saturday { get; set; }
    public DayAvailabilityDto? Sunday { get; set; }
}

public class FacilityDto
{
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
}

public class DayAvailabilityDto
{
    public WorkPeriodDto? WorkPeriod { get; set; }
    public List<TimeSlotDto>? BusySlots { get; set; }
}

public class WorkPeriodDto
{
    public int StartHour { get; set; }
    public int LunchStartHour { get; set; }
    public int LunchEndHour { get; set; }
    public int EndHour { get; set; }
}

public class TimeSlotDto
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}
