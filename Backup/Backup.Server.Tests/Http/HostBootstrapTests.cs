using System.Net;

namespace Backup.Server.Tests.Http;

public sealed class HostBootstrapTests
{
    [Fact]
    public async Task Host_boots_and_unauthenticated_me_returns_401()
    {
        await using var factory = new AuthApiFactory();
        var client = factory.CreateClient();

        var resp = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Seeded_user_can_log_in()
    {
        await using var factory = new AuthApiFactory();
        await factory.SeedUserAsync("alice", "S3cret-pass!", Backup.Server.Domain.Enums.AppUserRole.Admin);
        var client = factory.CreateClient();

        var resp = await AuthTestHelpers.LoginAsync(client, "alice", "S3cret-pass!");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
