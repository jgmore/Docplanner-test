
namespace Docplanner.Tests;

public class CommonTestsFunctionality
{
    public static void SetRandomForwardedIp(HttpClient client)
    {
        var random = new Random();
        var ip = $"127.0.0.{random.Next(1, 254)}"; // Random localhost IP
        client.DefaultRequestHeaders.Remove("X-Forwarded-For");
        client.DefaultRequestHeaders.Add("X-Forwarded-For", ip);
    }
}