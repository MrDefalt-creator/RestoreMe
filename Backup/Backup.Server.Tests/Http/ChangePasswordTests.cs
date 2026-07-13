using System.Net;
using System.Net.Http.Json;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Tests.Http;

public sealed class ChangePasswordTests
{
    private const string User = "frank";
    private const string Pass = "Old-pass-4!";

    private static async Task<(AuthApiFactory factory, HttpClient client)> LoggedInAsync()
    {
        var factory = new AuthApiFactory();
        await factory.SeedUserAsync(User, Pass, AppUserRole.Admin);
        var client = factory.CreateClient();
        var login = await AuthTestHelpers.LoginAsync(client, User, Pass);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (factory, client);
    }

    [Fact]
    public async Task Change_password_succeeds_and_keeps_current_device_signed_in()
    {
        var (factory, client) = await LoggedInAsync();
        await using var _ = factory;

        var resp = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = Pass,
            newPassword = "Brand-new-pass-5!",
        });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // The rotated access cookie keeps the current device authenticated.
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task Change_password_with_wrong_current_password_returns_401()
    {
        var (factory, client) = await LoggedInAsync();
        await using var _ = factory;

        var resp = await client.PostAsJsonAsync("/api/auth/change-password", new
        {
            currentPassword = "not-the-current-password",
            newPassword = "Brand-new-pass-5!",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
