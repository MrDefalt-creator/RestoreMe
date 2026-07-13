using System.Net;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Tests.Http;

public sealed class AuthzTests
{
    [Theory]
    [InlineData("/api/auth/me")]
    [InlineData("/api/auth/sessions")]
    public async Task Protected_endpoint_without_cookie_returns_401(string path)
    {
        await using var factory = new AuthApiFactory();
        var client = factory.CreateClient();

        var resp = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_with_valid_cookie_returns_200()
    {
        await using var factory = new AuthApiFactory();
        await factory.SeedUserAsync("grace", "Authz-pass-6!", AppUserRole.Viewer);
        var client = factory.CreateClient();
        await AuthTestHelpers.LoginAsync(client, "grace", "Authz-pass-6!");

        var resp = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }
}
