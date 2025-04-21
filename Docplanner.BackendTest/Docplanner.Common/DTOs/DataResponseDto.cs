
namespace Docplanner.Common.DTOs;

public class DataResponseDto
{
    public string FacilityId { get; set; }
    public IEnumerable<AvailabilitySlotDto> AvailableSlots { get; set; }
}

