using System.Net.Http.Json;

namespace Backup.Server.Tests.Http;

public static class AuthTestHelpers
{
    public static Task<HttpResponseMessage> LoginAsync(
        HttpClient client, string username, string password, bool rememberMe = false)
        => client.PostAsJsonAsync("/api/auth/login", new
        {
            username,
            password,
            rememberMe,
        });

    public static string? GetSetCookie(HttpResponseMessage resp, string name)
    {
        if (!resp.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }
        foreach (var c in cookies)
        {
            if (c.StartsWith(name + "=", StringComparison.Ordinal))
            {
                return c.Substring(name.Length + 1).Split(';')[0];
            }
        }
        return null;
    }

    public static bool SetCookieHasAttribute(HttpResponseMessage resp, string name, string attribute)
    {
        if (!resp.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return false;
        }
        foreach (var c in cookies)
        {
            if (c.StartsWith(name + "=", StringComparison.Ordinal))
            {
                return c.Contains(attribute, StringComparison.OrdinalIgnoreCase);
            }
        }
        return false;
    }

    public static Task<HttpResponseMessage> PostWithRefreshCookieAsync(
        HttpClient client, string path, string refreshToken)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, path);
        req.Headers.Add("Cookie", $"refresh_token={refreshToken}");
        return client.SendAsync(req);
    }
}
