using System.Security.Claims;

namespace Backup.Server.Api.Security;

public static class ClaimsPrincipalExtensions
{
    public static Guid? TryGetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static Guid? TryGetAgentId(this ClaimsPrincipal principal)
    {
        if (!principal.IsAgent())
        {
            return null;
        }

        return principal.TryGetUserId();
    }

    public static bool IsAgent(this ClaimsPrincipal principal)
    {
        return string.Equals(
            principal.FindFirstValue(AuthConstants.TokenTypeClaim),
            AuthConstants.AgentTokenType,
            StringComparison.Ordinal);
    }
}
