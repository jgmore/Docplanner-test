using Docplanner.API.Middleware;
using Docplanner.Application.Interfaces;
using Docplanner.Application.Services;
using Docplanner.Common.Converters;
using Docplanner.Infrastructure.Configuration;
using dotenv.net;
using Microsoft.Extensions.Options;
using System.Text.Json;

// Load .env file into environment
DotEnv.Load();

var builder = WebApplication.CreateBuilder(args);

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
});

builder.Services.Configure<SlotApiOptions>(builder.Configuration.GetSection("SlotApi"));
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();
builder.Services.AddHttpClient<ISlotServiceAdapter, SlotServiceAdapter>((provider, client) =>
{
    var options = provider.GetRequiredService<IOptions<SlotApiOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl); // make sure this is an absolute URI
});

builder.Services.AddScoped<ISlotService, SlotService>();

builder.Services.AddLogging();
var app = builder.Build();

// Registrar middleware de manejo de errores (debe ir primero)
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.UseOpenApi();    // serve OpenAPI/Swagger documents
app.UseSwaggerUi(); // serve Swagger UI

app.MapControllers();
app.Run();

public partial class Program { }