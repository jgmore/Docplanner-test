using Docplanner.API.Middleware;
using Docplanner.Application.Interfaces;
using Docplanner.Application.Services;
using Docplanner.Common.Converters;
using Docplanner.Infrastructure.Configuration;
using dotenv.net;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation.AspNetCore;
using Docplanner.Application.Validators;
using FluentValidation;
using Docplanner.Common.DTOs;
using System.Net;

static bool IsPrivateIp(IPAddress ipAddress)
{
    if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
    {
        var bytes = ipAddress.GetAddressBytes();
        return (bytes[0] == 10)
            || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            || (bytes[0] == 192 && bytes[1] == 168);
    }
    return false;
}


// Load .env file into environment
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

// Load user credentials
var rawUsers = Environment.GetEnvironmentVariable("AUTH_USERS"); // Authentication credentials are stored in a environment variable for simplicity, this can be implemented in different ways for a more robust application, from having the credentials in a database to have a connection to another API where the credentials are checked or even both for two force authentication.
var userCredentials = rawUsers?
    .Split(',', StringSplitOptions.RemoveEmptyEntries)
    .Select(entry => entry.Split(':'))
    .Where(parts => parts.Length == 2)
    .Select(parts => new UserCredentialDto
    {
        Username = parts[0].Trim(),
        Password = PasswordHasher.Hash(parts[1].Trim())
    })
    .ToList() ?? new List<UserCredentialDto>();

builder.Services.AddSingleton(userCredentials);

// Configure JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

// Used to skip rate limit on tests
var useForwardedHeaders = builder.Environment.IsEnvironment("Testing"); // Create an environment called "Testing" for tests
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

// Add CORS policy
var allowedOrigin = builder.Configuration.GetSection("AllowedCorsOrigins").Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("SwaggerOnly", policy =>
    {
        policy.WithOrigins(allowedOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
// Add Fluent Validation
builder.Services.AddValidatorsFromAssemblyContaining<BookingRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

        // Register Custom Converters
        options.JsonSerializerOptions.Converters.Add(new DateTimeJsonConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerDocument(config =>
{
    config.Title = "Docplanner Tech Test";
    config.Description = "API for doctor slots availability and booking doctor slots";

    // JWT Authentication support in Swagger UI
    config.AddSecurity("JWT", new NSwag.OpenApiSecurityScheme
    {
        Type = NSwag.OpenApiSecuritySchemeType.ApiKey,
        Name = "Authorization",
        In = NSwag.OpenApiSecurityApiKeyLocation.Header,
        Description = "Enter: Bearer {your JWT token}"
    });

    config.OperationProcessors.Add(
        new NSwag.Generation.Processors.Security.OperationSecurityScopeProcessor("JWT"));
});

builder.Services.AddMemoryCache();

var rateLimitSection = builder.Configuration.GetSection("RateLimiting");
int tokenLimit = rateLimitSection.GetValue<int>("TokenLimit");
int tokensPerPeriod = rateLimitSection.GetValue<int>("TokensPerPeriod");
int replenishmentSeconds = rateLimitSection.GetValue<int>("ReplenishmentSeconds");

// Configure rate limits per Ip
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("PerIpPolicy", httpContext =>
    {
        // Prefer X-Forwarded-For if present and private
        var remoteIp = httpContext.Connection.RemoteIpAddress;
        var ip = remoteIp is { } && IsPrivateIp(remoteIp)
            ? httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? remoteIp.ToString()
            : remoteIp?.ToString() ?? "unknown";

        return RateLimitPartition.GetTokenBucketLimiter(ip, key => new TokenBucketRateLimiterOptions
        {
            TokenLimit = tokenLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            ReplenishmentPeriod = TimeSpan.FromSeconds(replenishmentSeconds),
            TokensPerPeriod = tokensPerPeriod,
            AutoReplenishment = true
        });
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = (context, token) =>
    {
        var ip = context.HttpContext.Connection.RemoteIpAddress;
        context.HttpContext.Response.Headers["Retry-After"] = "10";
        Console.WriteLine($"Rate limit exceeded for IP: {ip}");
        return ValueTask.CompletedTask;
    };
});


builder.Services.Configure<SlotApiOptions>(builder.Configuration.GetSection("SlotApi"));
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<RetryPolicyOptions>(builder.Configuration.GetSection("RetryPolicyOptions"));


builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddHttpClient<ISlotServiceAdapter, SlotServiceAdapter>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<SlotApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl); // make sure this is an absolute URI
});

builder.Services.AddScoped<ISlotService, SlotService>();
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddLogging();
var app = builder.Build();

// Register error handling middleware
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsEnvironment("Testing"))
{
    app.UseForwardedHeaders();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("SwaggerOnly");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.UseOpenApi();    // serve OpenAPI/Swagger documents
app.UseSwaggerUi(); // serve Swagger UI

app.MapControllers().RequireRateLimiting("PerIpPolicy");
app.Run();

public partial class Program { }


