using System.Net;
using System.Text.Json;
using Docplanner.API.Middleware;
using Docplanner.Common.DTOs;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Docplanner.Tests.Unit;

public class ErrorHandlingMiddlewareTests
{
    private readonly Mock<ILogger<ErrorHandlingMiddleware>> _loggerMock;
    private readonly DefaultHttpContext _httpContext;

    public ErrorHandlingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<ErrorHandlingMiddleware>>();
        _httpContext = new DefaultHttpContext();
    }

    [Fact]
    public async Task InvokeAsync_WhenNoException_SetsCorrelationId()
    {
        // Arrange
        var middleware = new ErrorHandlingMiddleware(context => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.True(_httpContext.Response.Headers.ContainsKey("X-Correlation-ID"));
        Assert.True(_httpContext.Items.ContainsKey("CorrelationId"));
    }

    [Theory]
    [InlineData(typeof(ArgumentException), 400)]
    [InlineData(typeof(UnauthorizedAccessException), 401)]
    [InlineData(typeof(KeyNotFoundException), 404)]
    [InlineData(typeof(Exception), 500)]
    public async Task InvokeAsync_WhenExceptionThrown_ReturnsExpectedStatusCode(Type exceptionType, int expectedStatusCode)
    {
        // Arrange
        var middleware = new ErrorHandlingMiddleware(context => throw (Exception)Activator.CreateInstance(exceptionType)!, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal("application/json; charset=utf-8", _httpContext.Response.ContentType);
        Assert.Equal(expectedStatusCode, _httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_WhenCorrelationIdProvided_UsesProvidedId()
    {
        // Arrange
        var providedId = Guid.NewGuid().ToString();
        _httpContext.Request.Headers["X-Correlation-ID"] = providedId;

        var middleware = new ErrorHandlingMiddleware(context => Task.CompletedTask, _loggerMock.Object);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        Assert.Equal(providedId, _httpContext.Response.Headers["X-Correlation-ID"]);
        Assert.Equal(providedId, _httpContext.Items["CorrelationId"]);
    }
}
