using Backup.Server.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Backup.Server.Api.Filters;

// When a user JWT carries the MustChangePasswordClaim, only the
// password-change / self / logout endpoints are reachable. Everything
// else returns 403 with a machine-readable code so the UI can pop
// the rotation modal.
public sealed class MustChangePasswordFilter : IAsyncAuthorizationFilter
{
    private static readonly HashSet<string> AllowedPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/auth/me",
        "/api/auth/change-password",
        "/api/auth/logout",
    };

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var principal = context.HttpContext.User;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return Task.CompletedTask;
        }

        var mustChange = principal.FindFirst(AuthConstants.MustChangePasswordClaim)?.Value;
        if (string.IsNullOrEmpty(mustChange) || mustChange == "0")
        {
            return Task.CompletedTask;
        }

        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        if (AllowedPaths.Contains(path))
        {
            return Task.CompletedTask;
        }

        context.Result = new ObjectResult(new
        {
            message = "Password change required before further actions.",
            code = "must_change_password",
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };

        return Task.CompletedTask;
    }
}
