using System.Net;
using System.Net.Http.Json;
using Backup.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Tests.Http;

public sealed class SessionTests
{
    private const string User = "erin";
    private const string Pass = "Session-pass-3!";

    // Mirror of the server's SessionDto for deserialization.
    private sealed record SessionView(Guid Id, DateTime CreatedAtUtc, DateTime? LastUsedAtUtc,
        string? UserAgent, string? CreatedByIp, bool Current);

    private static async Task<(AuthApiFactory factory, HttpClient client)> LoggedInAsync()
    {
        var factory = new AuthApiFactory();
        await factory.SeedUserAsync(User, Pass, AppUserRole.Admin);
        var client = factory.CreateClient(); // HandleCookies = true
        var login = await AuthTestHelpers.LoginAsync(client, User, Pass);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (factory, client);
    }

    [Fact]
    public async Task Sessions_lists_the_current_session_flagged()
    {
        var (factory, client) = await LoggedInAsync();
        await using var _ = factory;

        var sessions = await client.GetFromJsonAsync<List<SessionView>>("/api/auth/sessions");

        Assert.NotNull(sessions);
        Assert.Single(sessions!);
        Assert.True(sessions![0].Current);
    }

    [Fact]
    public async Task Revoking_an_unknown_session_id_returns_404()
    {
        var (factory, client) = await LoggedInAsync();
        await using var _ = factory;

        var resp = await client.DeleteAsync($"/api/auth/sessions/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Revoking_own_session_removes_it()
    {
        var (factory, client) = await LoggedInAsync();
        await using var _ = factory;

        var sessions = await client.GetFromJsonAsync<List<SessionView>>("/api/auth/sessions");
        var id = sessions![0].Id;

        var del = await client.DeleteAsync($"/api/auth/sessions/{id}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        // The session's family is revoked -> it can no longer refresh.
        var refresh = await client.PostAsync("/api/auth/refresh", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_the_presented_family()
    {
        var (factory, client) = await LoggedInAsync();
        await using var _ = factory;

        var logout = await client.PostAsync("/api/auth/logout", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        // No active sessions remain for the user.
        var remaining = await factory.InScopeAsync(db =>
            db.RefreshTokens.CountAsync(t => t.RevokedAtUtc == null));
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task Logout_all_revokes_every_session()
    {
        var (factory, client) = await LoggedInAsync();
        await using var _ = factory;

        // Add a second, independent session for the same user (second login).
        var client2 = factory.CreateClient();
        await AuthTestHelpers.LoginAsync(client2, User, Pass);

        var logoutAll = await client.PostAsync("/api/auth/logout-all", content: null);
        Assert.Equal(HttpStatusCode.NoContent, logoutAll.StatusCode);

        var active = await factory.InScopeAsync(db =>
            db.RefreshTokens.CountAsync(t => t.RevokedAtUtc == null));
        Assert.Equal(0, active);
    }
}
