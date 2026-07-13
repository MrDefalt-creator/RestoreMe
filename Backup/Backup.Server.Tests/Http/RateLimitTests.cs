using System.Net;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Tests.Http;

public sealed class RateLimitTests
{
    [Fact]
    public async Task Eleventh_login_in_the_window_is_rate_limited()
    {
        // Own factory instance = own per-host limiter budget. Valid logins (which
        // don't lock the account) isolate pure per-IP rate limiting from lockout.
        await using var factory = new AuthApiFactory();
        await factory.SeedUserAsync("carol", "Valid-pass-9!", AppUserRole.Admin);
        var client = factory.CreateClient();

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 11; i++)
        {
            var resp = await AuthTestHelpers.LoginAsync(client, "carol", "Valid-pass-9!");
            statuses.Add(resp.StatusCode);
        }

        // First 10 succeed, the 11th trips the sliding-window limiter (permit = 10).
        Assert.Equal(10, statuses.Count(s => s == HttpStatusCode.OK));
        Assert.Equal(HttpStatusCode.TooManyRequests, statuses[10]);
    }
}
