using System.Net;
using Backup.Server.Api.Security;
using Backup.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Tests.Http;

public sealed class RefreshTests
{
    private const string User = "dave";
    private const string Pass = "Refresh-pass-2!";

    private static async Task<(AuthApiFactory factory, HttpClient client)> LoggedOutClientAsync()
    {
        var factory = new AuthApiFactory();
        await factory.SeedUserAsync(User, Pass, AppUserRole.Admin);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        return (factory, client);
    }

    [Fact]
    public async Task Refresh_rotates_the_refresh_token()
    {
        var (factory, client) = await LoggedOutClientAsync();
        await using var _ = factory;

        var login = await AuthTestHelpers.LoginAsync(client, User, Pass);
        var firstRefresh = AuthTestHelpers.GetSetCookie(login, "refresh_token")!;

        var refresh = await AuthTestHelpers.PostWithRefreshCookieAsync(client, "/api/auth/refresh", firstRefresh);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);

        var rotated = AuthTestHelpers.GetSetCookie(refresh, "refresh_token")!;
        Assert.NotEqual(firstRefresh, rotated);
    }

    [Fact]
    public async Task Replaying_a_rotated_token_is_reuse_detected_and_burns_the_family()
    {
        var (factory, client) = await LoggedOutClientAsync();
        await using var _ = factory;

        var login = await AuthTestHelpers.LoginAsync(client, User, Pass);
        var original = AuthTestHelpers.GetSetCookie(login, "refresh_token")!;

        // Rotate once — `original` is now spent.
        var firstRotate = await AuthTestHelpers.PostWithRefreshCookieAsync(client, "/api/auth/refresh", original);
        Assert.Equal(HttpStatusCode.OK, firstRotate.StatusCode);
        var rotated = AuthTestHelpers.GetSetCookie(firstRotate, "refresh_token")!;

        // Push the original's revocation outside the reuse grace window, so replaying
        // it reads as a genuine replay (not a benign race/retry that would be tolerated).
        await factory.InScopeAsync(async db =>
        {
            var hash = RefreshTokenService.Hash(original);
            var row = await db.RefreshTokens.FirstAsync(t => t.TokenHash == hash);
            row.RevokedAtUtc = DateTime.UtcNow.AddHours(-1);
            await db.SaveChangesAsync();
            return true;
        });

        // Replay the spent original -> reuse detected -> 401.
        var replay = await AuthTestHelpers.PostWithRefreshCookieAsync(client, "/api/auth/refresh", original);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);

        // The whole family is burned: even the freshly rotated token no longer works.
        var afterBurn = await AuthTestHelpers.PostWithRefreshCookieAsync(client, "/api/auth/refresh", rotated);
        Assert.Equal(HttpStatusCode.Unauthorized, afterBurn.StatusCode);

        // Reuse was audit-logged.
        var reuseLogged = await factory.InScopeAsync(db =>
            db.AuditLogs.AnyAsync(a => a.Action == "auth.refresh_reuse_detected"));
        Assert.True(reuseLogged);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_returns_401()
    {
        var (factory, client) = await LoggedOutClientAsync();
        await using var _ = factory;

        var resp = await client.PostAsync("/api/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Session_only_login_is_not_upgraded_to_persistent_on_refresh()
    {
        var (factory, client) = await LoggedOutClientAsync();
        await using var _ = factory;

        var login = await AuthTestHelpers.LoginAsync(client, User, Pass, rememberMe: false);
        var refreshToken = AuthTestHelpers.GetSetCookie(login, "refresh_token")!;

        var refresh = await AuthTestHelpers.PostWithRefreshCookieAsync(client, "/api/auth/refresh", refreshToken);

        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        // Session-only must stay session-only across rotation (no Expires/Max-Age).
        Assert.False(AuthTestHelpers.SetCookieHasAttribute(refresh, "refresh_token", "expires"));
        Assert.False(AuthTestHelpers.SetCookieHasAttribute(refresh, "refresh_token", "max-age"));
    }
}
