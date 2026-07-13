using System.Net;
using Backup.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Backup.Server.Tests.Http;

public sealed class LoginTests
{
    private const string User = "alice";
    private const string Pass = "S3cret-pass!";

    private static async Task<AuthApiFactory> NewFactoryWithUserAsync()
    {
        var factory = new AuthApiFactory();
        await factory.SeedUserAsync(User, Pass, AppUserRole.Admin);
        return factory;
    }

    private static HttpClient RawCookieClient(AuthApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    [Fact]
    public async Task Valid_login_sets_both_cookies_with_expected_attributes()
    {
        await using var factory = await NewFactoryWithUserAsync();
        var client = RawCookieClient(factory);

        var resp = await AuthTestHelpers.LoginAsync(client, User, Pass);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.NotNull(AuthTestHelpers.GetSetCookie(resp, "access_token"));
        Assert.NotNull(AuthTestHelpers.GetSetCookie(resp, "refresh_token"));
        Assert.True(AuthTestHelpers.SetCookieHasAttribute(resp, "access_token", "httponly"));
        Assert.True(AuthTestHelpers.SetCookieHasAttribute(resp, "access_token", "path=/"));
        Assert.True(AuthTestHelpers.SetCookieHasAttribute(resp, "refresh_token", "httponly"));
        Assert.True(AuthTestHelpers.SetCookieHasAttribute(resp, "refresh_token", "path=/api/auth"));
    }

    [Fact]
    public async Task Wrong_password_returns_401_and_sets_no_cookies()
    {
        await using var factory = await NewFactoryWithUserAsync();
        var client = RawCookieClient(factory);

        var resp = await AuthTestHelpers.LoginAsync(client, User, "wrong-password");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Null(AuthTestHelpers.GetSetCookie(resp, "access_token"));
    }

    [Fact]
    public async Task Remember_me_true_persists_refresh_cookie_with_expires()
    {
        await using var factory = await NewFactoryWithUserAsync();
        var client = RawCookieClient(factory);

        var resp = await AuthTestHelpers.LoginAsync(client, User, Pass, rememberMe: true);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.True(AuthTestHelpers.SetCookieHasAttribute(resp, "refresh_token", "expires"));
    }

    [Fact]
    public async Task Remember_me_false_leaves_refresh_cookie_session_scoped()
    {
        await using var factory = await NewFactoryWithUserAsync();
        var client = RawCookieClient(factory);

        var resp = await AuthTestHelpers.LoginAsync(client, User, Pass, rememberMe: false);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.False(AuthTestHelpers.SetCookieHasAttribute(resp, "refresh_token", "expires"));
        Assert.False(AuthTestHelpers.SetCookieHasAttribute(resp, "refresh_token", "max-age"));
    }
}
