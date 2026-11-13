using System.Net;
using System.Net.Http.Json;

namespace Api.Tests;

/// <summary>Separate API instance with a low limit, so it doesn't affect the other tests.</summary>
public class LowRateLimitFactory : ApiFactory
{
    protected override int AuthPerMinute => 3;
}

public class RateLimitTests(LowRateLimitFactory factory) : IClassFixture<LowRateLimitFactory>
{
    [Fact]
    public async Task Too_many_login_attempts_return_429()
    {
        var client = factory.CreateClient();
        var attempt = new { email = "attacker@example.com", password = "guess-123" };

        for (var i = 0; i < 3; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", attempt);
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }

        var blocked = await client.PostAsJsonAsync("/api/auth/login", attempt);
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
    }
}
