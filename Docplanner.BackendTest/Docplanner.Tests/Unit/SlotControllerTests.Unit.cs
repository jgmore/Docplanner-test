using Xunit;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Docplanner.API.Controllers;
using Docplanner.Common.DTOs;
using Docplanner.Application.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Docplanner.Tests.Unit;

[Trait("Category", "Unit")]
public class SlotControllerTests
{
    private readonly Mock<ISlotService> _serviceMock = new();

    private SlotsController CreateController() =>
        new(_serviceMock.Object);

    [Fact]
    public async Task GetWeeklyAvailability_ReturnsOkResult()
    {
        var response = ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateSuccess(
                "Id1",
                new[] { new AvailabilitySlotDto { Start = new DateTime(2025, 4, 15, 10, 00, 00), End = new DateTime(2025, 4, 15, 10, 20, 00) } },
                $"Retrieved 1 slots for week starting 20250415"
            );
        _serviceMock.Setup(s => s.GetWeeklyAvailabilityAsync("20250415"))
            .ReturnsAsync(response);
        
        var controller = CreateController();
        var result = await controller.GetWeeklyAvailability("20250415");

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<ApiResponseDto<IEnumerable<AvailabilitySlotDto>>>(okResult.Value);
        Assert.True(value.Success);
        Assert.NotEmpty(value.Data);
    }

    [Fact]
    public async Task GetWeeklyAvailability_ReturnsOk_WithEmptySlotList()
    {
        // Arrange
        var monday = "20250422";

        var serviceResponse = ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateSuccess(
            facilityId: "facility123",
            data: Enumerable.Empty<AvailabilitySlotDto>(),
            message: "No available slots this week"
        );

        _serviceMock.Setup(s => s.GetWeeklyAvailabilityAsync(monday))
            .ReturnsAsync(serviceResponse);

        var controller = CreateController();

        // Act
        var result = await controller.GetWeeklyAvailability(monday);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<IEnumerable<AvailabilitySlotDto>>>(okResult.Value);
        Assert.True(response.Success);
        Assert.Equal("facility123", response.FacilityId);
        Assert.Equal("No available slots this week", response.Message);
        Assert.NotNull(response.Data);
        Assert.Empty(response.Data!);
    }


    [Theory]
    [InlineData("Unexpected error", new string[] { })]
    [InlineData("Internal error occurred", new string[] { "Database timeout", "Unexpected null value" })]
    public async Task GetWeeklyAvailability_ReturnsInternalServerError_WhenServiceFails(string message, string[] errors)
    {
        // Arrange
        var monday = "20250422";

        var serviceResponse = ApiResponseDto<IEnumerable<AvailabilitySlotDto>>.CreateError(
            message: message,
            errors: errors.Length > 0 ? errors : null
        );

        _serviceMock.Setup(s => s.GetWeeklyAvailabilityAsync(monday))
            .ReturnsAsync(serviceResponse);

        var controller = CreateController();

        // Act
        var result = await controller.GetWeeklyAvailability(monday);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponseDto<IEnumerable<AvailabilitySlotDto>>>(objectResult.Value);
        Assert.False(response.Success);
        Assert.Equal(message, response.Message);

        if (errors.Length > 0)
        {
            Assert.NotNull(response.Errors);
            foreach (var error in errors)
            {
                Assert.Contains(error, response.Errors!);
            }
        }
    }

    [Fact]
    public async Task BookSlot_ReturnsOk_OnSuccess()
    {
        var patient = new PatientDto { Email = "user@test.com", Name = "user", Phone= "600000000", SecondName ="test" };
        var request = new BookingRequestDto { Start = "10:00", End = "10:20", Patient = patient, FacilityId = "Id1", Comments = "Unit Test" };

        _serviceMock.Setup(s => s.BookSlotAsync(request))
            .ReturnsAsync(new ApiResponseDto<bool> { Success = true });

        var controller = CreateController();
        var result = await controller.BookSlot(request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var value = Assert.IsType<ApiResponseDto<bool>>(okResult.Value);
        Assert.True(value.Success);
    }

    [Fact]
    public async Task BookSlot_ReturnsBadRequest_OnFailure()
    {
        var patient = new PatientDto { Email = "user@test.com", Name = "user", Phone = "600000000", SecondName = "test" };
        var request = new BookingRequestDto { Start = "10:00", End = "10:20", Patient = patient };

        _serviceMock.Setup(s => s.BookSlotAsync(request))
            .ReturnsAsync(new ApiResponseDto<bool> { Success = false, Message = "Slot does not exist." });

        var controller = CreateController();
        var result = await controller.BookSlot(request);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var value = Assert.IsType<ApiResponseDto<bool>>(badRequest.Value);
        Assert.False(value.Success);
    }

    private void ValidateModel(object model, ControllerBase controller)
    {
        var validationContext = new ValidationContext(model);
        var validationResults = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);

        if (!isValid)
        {
            foreach (var validationResult in validationResults)
            {
                var memberNames = validationResult.MemberNames.Any()
                    ? validationResult.MemberNames
                    : new[] { string.Empty }; // fallback for general model errors

                foreach (var memberName in memberNames)
                {
                    controller.ModelState.AddModelError(memberName, validationResult.ErrorMessage);
                }
            }
        }
    }


    [Fact]
    public async Task BookSlot_ReturnsBadRequest_WhenModelStateIsInvalid()
    {
        // Arrange
        var controller = CreateController();

        var request = new BookingRequestDto
        {
            FacilityId = "",
            Start = "10:00",
            End = "10:20",
            Comments = "Some comment",
            Patient = new PatientDto
            {
                Name = "Jane",
                SecondName = "Doe",
                Email = "", 
                Phone = "123456789"
            }
        };

        ValidateModel(request, controller);
        // Act
        var result = await controller.BookSlot(request);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var errors = Assert.IsType<SerializableError>(badRequestResult.Value);
        Assert.True(errors.ContainsKey("FacilityId"));

        // Ensure the service was NOT called
        _serviceMock.Verify(s => s.BookSlotAsync(It.IsAny<BookingRequestDto>()), Times.Never);
    }

    [Theory]
    [InlineData("Validation failed", new string[] { "Start time is invalid", "Patient email format is wrong" })]
    [InlineData("Booking failed", new string[] { "Slot already booked" })]
    public async Task BookSlot_ReturnsBadRequest_WhenServiceFails(string message, string[] errors)
    {
        // Arrange
        var controller = CreateController();

        var request = new BookingRequestDto
        {
            FacilityId = "f123",
            Start = "10:00",
            End = "10:20",
            Comments = "Comment",
            Patient = new PatientDto
            {
                Name = "Test",
                SecondName = "User",
                Email = "test@example.com",
                Phone = "123456789"
            }
        };

        var serviceResponse = ApiResponseDto<bool>.CreateError(
            message: message,
            errors: errors
        );

        _serviceMock.Setup(s => s.BookSlotAsync(request))
            .ReturnsAsync(serviceResponse);

        // Act
        var result = await controller.BookSlot(request);

        // Assert
        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponseDto<bool>>(badRequest.Value);
        Assert.False(response.Success);
        Assert.Equal(message, response.Message);
        Assert.NotNull(response.Errors);

        foreach (var error in errors)
        {
            Assert.Contains(error, response.Errors!);
        }
    }

}
