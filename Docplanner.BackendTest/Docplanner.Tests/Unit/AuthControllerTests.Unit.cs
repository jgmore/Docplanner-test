using Docplanner.API.Controllers;
using Docplanner.Common.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;

namespace Docplanner.Tests.Unit;

[Trait("Category", "Unit")]
public class AuthControllerTests
{
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IConfiguration> _configMock;

    public AuthControllerTests()
    {
        _tokenServiceMock = new Mock<ITokenService>();
        _configMock = new Mock<IConfiguration>();
    }

    [Fact]
    public void Login_WithValidCredentials_ReturnsToken()
    {
        // Arrange
        var loginDto = new LoginDto { Username = "user1", Password = "pass1" };
        var hashedPassword = PasswordHasher.Hash(loginDto.Password);

        var credentials = new List<UserCredentialDto>
        {
            new UserCredentialDto { Username = "user1", Password = hashedPassword }
        };

        _tokenServiceMock.Setup(x => x.GenerateToken("user1")).Returns("fake-token");

        var controller = new AuthController(_tokenServiceMock.Object, _configMock.Object, credentials);

        // Act
        var result = controller.Login(loginDto);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);
        Assert.Contains("Token", okResult.Value!.ToString());
    }

    [Fact]
    public void Login_WithInvalidPassword_ReturnsUnauthorized()
    {
        // Arrange
        var loginDto = new LoginDto { Username = "user1", Password = "wrongpass" };
        var credentials = new List<UserCredentialDto>
        {
            new UserCredentialDto { Username = "user1", Password = PasswordHasher.Hash("correctpass") }
        };

        var controller = new AuthController(_tokenServiceMock.Object, _configMock.Object, credentials);

        // Act
        var result = controller.Login(loginDto);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public void Login_WithUnknownUsername_ReturnsUnauthorized()
    {
        // Arrange
        var loginDto = new LoginDto { Username = "unknown", Password = "any" };
        var credentials = new List<UserCredentialDto>
        {
            new UserCredentialDto { Username = "user1", Password = PasswordHasher.Hash("pass1") }
        };

        var controller = new AuthController(_tokenServiceMock.Object, _configMock.Object, credentials);

        // Act
        var result = controller.Login(loginDto);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Theory]
    [InlineData("", "pass1")] // Empty username
    [InlineData("user1", "")] // Empty password
    [InlineData(null!, "pass1")] // null username
    [InlineData("user1", null!)] // null password
    public void Login_WithEmptyUsernameOrPassword_ReturnsUnauthorized(string username, string password)
    {
        // Arrange
        var credentials = new List<UserCredentialDto>
        {
            new UserCredentialDto { Username = "user1", Password = PasswordHasher.Hash("pass1") }
        };
        // Act
        var controller = new AuthController(_tokenServiceMock.Object, _configMock.Object, credentials);        
        var result = controller.Login(new LoginDto { Username = "", Password = "pass1" });
        // Assert
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void Login_WithNoUsersConfigured_ReturnsUnauthorized()
    {
        // Arrange
        var emptyCredentials = new List<UserCredentialDto>(); // no users
        var controller = new AuthController(_tokenServiceMock.Object, _configMock.Object, emptyCredentials);

        var loginDto = new LoginDto { Username = "user1", Password = "pass1" };

        // Act
        var result = controller.Login(loginDto);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

}

