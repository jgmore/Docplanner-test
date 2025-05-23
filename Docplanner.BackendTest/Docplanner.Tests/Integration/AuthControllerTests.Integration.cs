﻿using Xunit;
using dotenv.net;
using System.Net.Http.Json;
using System.Net;
using System.Text;
using Docplanner.Common.DTOs;

namespace Docplanner.Tests.Integration;

[Trait("Category", "Integration")]
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
        // Arrange
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
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

        // Act
        var response = await _client.PostAsJsonAsync("/api/Auth/login", loginDto);

        // Assert
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("token", json);
    }

    [Fact]
    public async Task ProtectedEndpoint_ShouldReturnUnauthorized_WhenNoTokenProvided()
    {
        // Arrange
        CommonTestsFunctionality.SetRandomForwardedIp(_client);
        // Act
        var response = await _client.GetAsync("/api/slots/week/20250421");
        // Assert
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("GET", "/api/Auth/login")]
    [InlineData("PUT", "/api/Auth/login")]
    [InlineData("DELETE", "/api/Auth/login")]
    [InlineData("PATCH", "/api/Auth/login")]
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