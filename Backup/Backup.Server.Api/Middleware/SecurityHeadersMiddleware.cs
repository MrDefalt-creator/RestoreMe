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
        // Swagger UI / OpenAPI explorer renders HTML with inline scripts
        // and CDN assets; a "default-src 'none'" CSP would blank the page.
        // We still send the rest of the defensive headers for those routes.
        var path = context.Request.Path;
        var isSwaggerUi =
            path.StartsWithSegments("/swagger") ||
            path.StartsWithSegments("/openapi");

        context.Response.OnStarting(state =>
        {
            var ctx = (HeadersState)state;
            var h = ctx.Headers;
            SetIfMissing(h, "X-Content-Type-Options", "nosniff");
            SetIfMissing(h, "X-Frame-Options", "DENY");
            SetIfMissing(h, "Referrer-Policy", "strict-origin-when-cross-origin");
            SetIfMissing(h, "X-Permitted-Cross-Domain-Policies", "none");
            if (!ctx.IsSwaggerUi)
            {
                SetIfMissing(h, "Content-Security-Policy", "default-src 'none'; frame-ancestors 'none'");
            }
            return Task.CompletedTask;
        }, new HeadersState(context.Response.Headers, isSwaggerUi));

        return _next(context);
    }

    private sealed record HeadersState(IHeaderDictionary Headers, bool IsSwaggerUi);

    private static void SetIfMissing(IHeaderDictionary headers, string name, string value)
    {
        if (!headers.ContainsKey(name))
        {
            headers[name] = value;
        }
    }
}
