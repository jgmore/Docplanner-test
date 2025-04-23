using System.Net;
using System.Net.Http.Json;
using Docplanner.Common.Converters;
using System.Text.Json;
using Docplanner.Common.DTOs;
using Xunit;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using dotenv.net;

namespace Docplanner.Tests.Integration;

public class SlotControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly IConfiguration _configuration;

    public SlotControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env");
        DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] { envPath }));
    }

    private async Task<string> GetJwtTokenAsync()
    {
        LoginDto loginDto = new LoginDto();
        loginDto.Username = Environment.GetEnvironmentVariable("SlotApi__Username");
        loginDto.Password = Environment.GetEnvironmentVariable("SlotApi__Password");

        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginDto);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("token").GetString()!;
    }


    [Fact]
    public async Task GetWeeklyAvailability_ReturnsSuccess()
    {
        // Act
        var token = await GetJwtTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/api/slots/week/20250415");

        // Configura manualmente las opciones de serialización para los tests
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new DateTimeJsonConverter() }
        };

        var content = await response.Content.ReadFromJsonAsync<ApiResponseDto<IEnumerable<AvailabilitySlotDto>>>(options);

        Console.WriteLine($"StatusCode: {response.StatusCode}");
        Console.WriteLine($"Success: {content?.Success}");
        Console.WriteLine($"Message: {content?.Message}");

        if (content?.Data != null)
        {
            foreach (var slot in content.Data)
            {
                Console.WriteLine($"Slot: {slot.DayOfWeek} {slot.Start:yyyy-MM-dd HH:mm} - {slot.End:HH:mm}");
            }
        }

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.True(content!.Success);
        Assert.NotEmpty(content.Data);
    }

    [Fact]
    public async Task BookSlot_ReturnsBadRequest_WhenDataInvalid()
    {
        // Arrange - Creamos un request con datos inválidos
        var request = new BookingRequestDto
        {
            Start = "",
            End = "",
            Patient = new PatientDto
            {
                Email = "fake@example.com"
            }
        };

        // Act
        var token = await GetJwtTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/api/slots/book", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task BookSlot_ReturnsSuccess_WhenDataValid()
    {
        // Arrange - Creamos un request con datos válidos
        var request = new BookingRequestDto
        {
            FacilityId = "Id1",
            Start = "2025-04-15 10:00:00",
            End = "2025-04-15 10:20:00",
            Comments = "Test booking",
            Patient = new PatientDto
            {
                Name = "Test",
                SecondName = "User",
                Email = "test@example.com",
                Phone = "123456789"
            }
        };

        // Act
        var token = await GetJwtTokenAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.PostAsJsonAsync("/api/slots/book", request);
        var content = await response.Content.ReadFromJsonAsync<ApiResponseDto<bool>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.True(content!.Success);
        Assert.True(content.Data);
    }
}
