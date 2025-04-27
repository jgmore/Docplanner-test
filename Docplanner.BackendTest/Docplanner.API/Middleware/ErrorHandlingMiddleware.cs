using System.Net;
using Docplanner.Common.DTOs;

namespace Docplanner.API.Middleware;

public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault() ?? Guid.NewGuid().ToString();

        context.Response.Headers["X-Correlation-ID"] = correlationId;
        context.Items["CorrelationId"] = correlationId;
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred. Correlation ID: {correlationId}", correlationId);
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var code = HttpStatusCode.InternalServerError; // 500 by default

        if (exception is ArgumentException)
            code = HttpStatusCode.BadRequest; // 400
        else if (exception is UnauthorizedAccessException)
            code = HttpStatusCode.Unauthorized; // 401
        else if (exception is KeyNotFoundException)
            code = HttpStatusCode.NotFound; // 404

        var response = ApiResponseDto<object>.CreateError(
            "An error occurred processing your request",
            new[]
            {
                exception.Message 
                #if DEBUG
                , $"StackTrace: {exception.StackTrace}"
                #endif
            }
        );

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)code;

        await context.Response.WriteAsJsonAsync(response);
    }
}