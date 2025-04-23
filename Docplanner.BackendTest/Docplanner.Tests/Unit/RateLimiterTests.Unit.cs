using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Xunit;

namespace Docplanner.Tests.Unit;

public class RateLimiterTests
{
    [Fact]
    public void RateLimiter_ShouldCreatePolicy_ForIp()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("127.0.0.1");

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
}