using System.ComponentModel.DataAnnotations;

namespace Docplanner.Common.DTOs;

public class BookingRequestDto
{
    [Required]
    public string FacilityId { get; set; } = string.Empty;
    [Required]
    public string Start { get; set; } = string.Empty;
    [Required]
    public string End { get; set; } = string.Empty;
    public string Comments { get; set; } = string.Empty;
    public PatientDto Patient { get; set; } = new PatientDto();
}

public class PatientDto
{
    public string Name { get; set; } = string.Empty;
    public string SecondName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
