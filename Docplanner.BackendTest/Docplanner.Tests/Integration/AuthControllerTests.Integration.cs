using Xunit;
using dotenv.net;
using System.Net.Http.Json;

namespace Docplanner.Tests.Integration;

public class AuthControllerIntegrationTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerIntegrationTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".env");
        DotEnv.Load(options: new DotEnvOptions(envFilePaths: new[] { envPath }));
    }


    [Fact]
    public async Task Login_ShouldReturnToken_WhenCredentialsAreValid()
    {
        LoginDto loginDto = new LoginDto();
        loginDto.Username = Environment.GetEnvironmentVariable("SlotApi__Username");
        loginDto.Password = Environment.GetEnvironmentVariable("SlotApi__Password");

        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginDto);

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("token", json);
    }

    [Fact]
    public async Task ProtectedEndpoint_ShouldReturnUnauthorized_WhenNoTokenProvided()
    {
        var response = await _client.GetAsync("/api/slots/week/20250421");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}