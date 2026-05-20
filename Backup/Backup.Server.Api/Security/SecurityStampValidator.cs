using Backup.Server.Application.Interfaces;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Caching.Memory;

namespace Backup.Server.Api.Security;

// Verifies that a user JWT's stamp claim still matches the live DB value.
// Password change / role change bumps AppUser.SecurityStamp so any token
// minted before the bump is rejected on the next call. A 30 s in-memory
// cache keeps the DB hit rate negligible; cache TTL is the longest window
// in which a revoked token can still pass.
public static class SecurityStampValidator
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(30);

    public static async Task ValidateAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;
        if (principal is null)
        {
            context.Fail("Missing principal.");
            return;
        }

        var tokenType = principal.FindFirst(AuthConstants.TokenTypeClaim)?.Value;
        if (tokenType != AuthConstants.UserTokenType)
        {
            // Agent tokens don't carry a stamp claim; their revocation lives
            // on Agent.TokenVersion (H2).
            return;
        }

        var userIdClaim = principal.TryGetUserId();
        if (!userIdClaim.HasValue)
        {
            context.Fail("Missing user identifier.");
            return;
        }

        var stampClaim = principal.FindFirst(AuthConstants.SecurityStampClaim)?.Value;
        if (string.IsNullOrWhiteSpace(stampClaim) || !Guid.TryParse(stampClaim, out var tokenStamp))
        {
            // Old tokens issued before the H4 rollout have no stamp claim;
            // force a re-login by failing validation.
            context.Fail("Token is missing a security stamp.");
            return;
        }

        var cache = context.HttpContext.RequestServices.GetRequiredService<IMemoryCache>();
        var userRepository = context.HttpContext.RequestServices.GetRequiredService<IAppUserRepository>();

        var cacheKey = $"sec-stamp:{userIdClaim.Value}";
        var currentStamp = await cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await userRepository.GetSecurityStampAsync(userIdClaim.Value);
        });

        if (currentStamp is null || currentStamp.Value != tokenStamp)
        {
            context.Fail("Security stamp mismatch — token has been invalidated.");
        }
    }
}
