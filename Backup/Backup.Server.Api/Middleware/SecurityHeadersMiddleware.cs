namespace Backup.Server.Api.Middleware;

// Sets defensive response headers that don't depend on per-request state.
// API serves JSON, not HTML — so we keep the CSP minimal ("default-src
// 'none'") which still buys protection if someone opens an API URL in
// the browser, and we deny framing outright.
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // OnStarting fires just before the first byte is flushed so we
        // also cover routes that short-circuit before reaching MVC.
        context.Response.OnStarting(state =>
        {
            var h = (IHeaderDictionary)state;
            SetIfMissing(h, "X-Content-Type-Options", "nosniff");
            SetIfMissing(h, "X-Frame-Options", "DENY");
            SetIfMissing(h, "Referrer-Policy", "strict-origin-when-cross-origin");
            SetIfMissing(h, "X-Permitted-Cross-Domain-Policies", "none");
            SetIfMissing(h, "Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
            return Task.CompletedTask;
        }, headers);

        return _next(context);
    }

    private static void SetIfMissing(IHeaderDictionary headers, string name, string value)
    {
        if (!headers.ContainsKey(name))
        {
            headers[name] = value;
        }
    }
}
