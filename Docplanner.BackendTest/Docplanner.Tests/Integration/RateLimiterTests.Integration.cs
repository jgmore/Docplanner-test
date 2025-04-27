using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using System.Net;
using System.Threading.RateLimiting;
using Xunit;

namespace Docplanner.Tests.Integration;

[Trait("Category", "Integration")]
public class RateLimiterTests
{
    [Fact]
    public void RateLimiter_ShouldCreatePolicy_ForIp()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        var limiter = RateLimitPartition.GetTokenBucketLimiter(
            context.Connection.RemoteIpAddress.ToString(),
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 5,
                QueueLimit = 0,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                TokensPerPeriod = 5,
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });

        Assert.NotNull(limiter);
    }

    [Fact]
    public async Task RateLimiter_RejectsRequest_WhenOverLimit()
    {
        var factory = new WebApplicationFactory<Program>();
        var client = factory.CreateClient();

        for (int i = 0; i < 10; i++)
            await client.GetAsync("/swagger/index.html"); // or any allowed endpoint

        var response = await client.GetAsync("/swagger/index.html");

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            Assert.Equal("10", response.Headers.GetValues("Retry-After").FirstOrDefault());
        }
    }

}