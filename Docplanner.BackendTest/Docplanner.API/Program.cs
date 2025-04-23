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

// Load .env file into environment
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);


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

        // Registrar conversores personalizados
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

builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("PerIpPolicy", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
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

// Registrar middleware de manejo de errores (debe ir primero)
app.UseMiddleware<ErrorHandlingMiddleware>();

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