using Docplanner.Application.Interfaces;
using Docplanner.Infrastructure.Adapters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real ISlotServiceAdapter registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(ISlotServiceAdapter));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Register the mock adapter
            services.AddScoped<ISlotServiceAdapter, MockSlotServiceAdapter>();
        });
    }
}
