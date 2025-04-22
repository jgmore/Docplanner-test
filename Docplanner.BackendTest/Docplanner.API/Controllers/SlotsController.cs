using Docplanner.Common.DTOs;
using Docplanner.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Docplanner.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SlotsController : ControllerBase
{
    private readonly ISlotService _slotService;

    public SlotsController(ISlotService slotService)
    {
        _slotService = slotService;
    }

    [HttpGet("week/{monday:regex(^\\d{{8}}$)}")]
    [ProducesResponseType(typeof(ApiResponseDto<IEnumerable<AvailabilitySlotDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<object>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetWeeklyAvailability(string monday)
    {
        var response = await _slotService.GetWeeklyAvailabilityAsync(monday);

        if (response.Success)
        {
            return Ok(response);
        }

        return StatusCode(500, response);
    }

    [HttpPost("book")]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponseDto<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BookSlot([FromBody] BookingRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }
        var result = await _slotService.BookSlotAsync(request);

        if (result.Success)
        {
            return Ok(result);
        }

        return BadRequest(result);
    }
}