using Docplanner.Application.Interfaces;
using Docplanner.Common.DTOs;
using Docplanner.Infrastructure.Adapters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseContentRoot(Directory.GetCurrentDirectory()) // Ensures base path is the test project folder
            .ConfigureAppConfiguration((context, configBuilder) =>
            {
                configBuilder
                    .SetBasePath(Directory.GetCurrentDirectory()) // Important: Sets base path to test project
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                    .AddEnvironmentVariables();
            })
            .ConfigureServices(services =>
            {
                // Replace real adapter with mock
                var descriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(ISlotServiceAdapter));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddScoped<ISlotServiceAdapter, MockSlotServiceAdapter>();

                // Register test UserCredentialDto list using AUTH_USERS from environment
                services.AddSingleton(provider =>
                {
                    var rawUsers = Environment.GetEnvironmentVariable("AUTH_USERS");
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

                    return userCredentials;
                });
            });
    }
}
