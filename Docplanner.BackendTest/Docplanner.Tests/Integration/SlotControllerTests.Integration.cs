using System.Net;
using System.Net.Http.Json;
using Docplanner.Common.Converters;
using System.Text.Json;
using Docplanner.Common.DTOs;
using Xunit;
using System.Net.Http.Headers;
using dotenv.net;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Docplanner.Application.Interfaces;
using Moq;
using Microsoft.Extensions.DependencyInjection;

namespace Docplanner.Tests.Integration;

[Trait("Category", "Integration")]
public class SlotControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public SlotControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env");
        DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] { envPath }));
    }

    private async Task<string> GetJwtTokenAsync()
    {
        var rawUsers = Environment.GetEnvironmentVariable("AUTH_USERS");
        var firstUserCredential = rawUsers?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split(':'))
            .Where(parts => parts.Length == 2)
            .Select(parts => new UserCredentialDto
            {
                Username = parts[0].Trim(),
                Password = parts[1].Trim()
            })
            .FirstOrDefault();

        LoginDto loginDto = new LoginDto();
        loginDto.Username = firstUserCredential.Username;
        loginDto.Password = firstUserCredential.Password;

        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginDto);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("token").GetString()!;
    }

    private async Task<bool> AddAuthenticationToken(HttpClient client)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetJwtTokenAsync());
        return true;
    }


    [Fact]
    public async Task GetWeeklyAvailability_ReturnsSuccess()
    {
        // Arrange
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
        // Act
        await AddAuthenticationToken(_client);

        var response = await _client.GetAsync("/api/slots/week/20250421");

        // Configure manually the serialization options
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
    public async Task GetWeeklyAvailability_Unauthorized_WithoutToken()
    {
        // Arrange
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
        var monday = "20250421";

        // Act
        var response = await _client.GetAsync($"/api/slots/week/{monday}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWeeklyAvailability_ReturnsError_WhenExternalServiceFails()
    {
        // Arrange
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
        var factory = new TestWebApplicationFactory();

        var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace ISlotServiceAdapter with a throwing mock
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ISlotServiceAdapter));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                var mockAdapter = new Mock<ISlotServiceAdapter>();
                mockAdapter.Setup(m => m.FetchWeeklyAvailabilityAsync(It.IsAny<string>()))
                           .ThrowsAsync(new ApplicationException("Simulated external service failure"));

                services.AddScoped(_ => mockAdapter.Object);
            });
        }).CreateClient();

        await AddAuthenticationToken(client);

        var monday = "20250422";

        // Act
        var response = await client.GetAsync($"/api/slots/week/{monday}");

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }



    [Fact]
    public async Task BookSlot_ReturnsBadRequest_WhenDataInvalid()
    {
        // Arrange
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
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
        await AddAuthenticationToken(_client);
        var response = await _client.PostAsJsonAsync("/api/slots/book", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.IsSuccessStatusCode);
    }

    [Fact]
    public async Task BookSlot_ReturnsSuccess_WhenDataValid()
    {
        // Arrange
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
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
        await AddAuthenticationToken(_client);
        var response = await _client.PostAsJsonAsync("/api/slots/book", request);
        var content = await response.Content.ReadFromJsonAsync<ApiResponseDto<bool>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(content);
        Assert.True(content!.Success);
        Assert.True(content.Data);
    }

    [Fact]
    public async Task BookSlot_ReturnsBadRequest_WhenPayloadIsInvalid()
    {
        // Arrange
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
        await AddAuthenticationToken(_client);

        var invalidRequestJson = "{}"; // Missing required fields

        var content = new StringContent(invalidRequestJson, Encoding.UTF8, "application/json");

        // Act
        var response = await _client.PostAsync("/api/slots/book", content);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }


    [Fact]
    public async Task BookAndGetSlots_RoundTrip()
    {
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
        await AddAuthenticationToken(_client);

        var responseGet = await _client.GetAsync("/api/slots/week/20250421");

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new DateTimeJsonConverter() }
        };

        var contentGet = await responseGet.Content.ReadFromJsonAsync<ApiResponseDto<IEnumerable<AvailabilitySlotDto>>>(options);

        Assert.Equal(HttpStatusCode.OK, responseGet.StatusCode);
        Assert.NotNull(contentGet);
        Assert.True(contentGet!.Success);
        Assert.NotEmpty(contentGet.Data);


        var request = new BookingRequestDto
        {
            FacilityId = contentGet.FacilityId,
            Start = contentGet.Data.First().Start.ToString("yyyy-MM-dd HH:mm:ss"),
            End = contentGet.Data.First().End.ToString("yyyy-MM-dd HH:mm:ss"),
            Comments = "Test booking",
            Patient = new PatientDto
            {
                Name = "Test",
                SecondName = "User",
                Email = "test@example.com",
                Phone = "123456789"
            }
        };

        var responsePost = await _client.PostAsJsonAsync("/api/slots/book", request);
        var contentPost = await responsePost.Content.ReadFromJsonAsync<ApiResponseDto<bool>>();

        // Assert
        Assert.Equal(HttpStatusCode.OK, responsePost.StatusCode);
        Assert.NotNull(contentPost);
        Assert.True(contentPost!.Success);
        Assert.True(contentPost.Data);
    }

    [Theory]
    [InlineData("POST", "/api/slots/week/20250421")]
    [InlineData("PUT", "/api/slots/week/20250421")]
    [InlineData("DELETE", "/api/slots/week/20250421")]
    [InlineData("PATCH", "/api/slots/week/20250421")]
    public async Task WeekEndpoint_ShouldReturn_405_ForUnsupportedVerbs(string method, string url)
    {
        // Arrange
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/slots/book")]
    [InlineData("PUT", "/api/slots/book")]
    [InlineData("DELETE", "/api/slots/book")]
    [InlineData("PATCH", "/api/slots/book")]
    public async Task BookEndpoint_ShouldReturn_405_ForUnsupportedVerbs(string method, string url)
    {
        // Arrange
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
        // Act
        var request = new HttpRequestMessage(new HttpMethod(method), url)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        var response = await _client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}
