using Backup.Server.Api.Security;
using Backup.Server.Api.Services;
using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly RefreshTokenService _refreshTokens;
    private readonly IRefreshTokenRepository _refreshRepo;
    private readonly IAppUserRepository _users;
    private readonly TokenService _tokenService;
    private readonly IAuditLogRepository _audit;
    private readonly IWebHostEnvironment _env;

    public AuthController(
        AuthService authService,
        RefreshTokenService refreshTokens,
        IRefreshTokenRepository refreshRepo,
        IAppUserRepository users,
        TokenService tokenService,
        IAuditLogRepository audit,
        IWebHostEnvironment env)
    {
        _authService = authService;
        _refreshTokens = refreshTokens;
        _refreshRepo = refreshRepo;
        _users = users;
        _tokenService = tokenService;
        _audit = audit;
        _env = env;
    }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request.Username, request.Password);

            // Start a fresh rotation family for this session and issue its first
            // refresh token. Remember-me only governs how long the refresh cookie
            // persists — the access cookie is always session-scoped because the
            // access token self-expires in minutes.
            var familyId = Guid.NewGuid();
            var refresh = await _refreshTokens.IssueAsync(
                result.User.Id,
                familyId,
                Request.Headers.UserAgent.ToString(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.RequestAborted,
                persistent: request.RememberMe);
            await _refreshRepo.SaveChangesAsync(HttpContext.RequestAborted);

            AppendAccessCookie(result.AccessToken);
            AppendRefreshCookie(refresh.RawToken, request.RememberMe, refresh.ExpiresAtUtc);

            return Ok(new { user = result.User });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        // Revoke exactly this session (its whole rotation family) so the
        // presented refresh token can never be rotated again — logging out is
        // server-side, not just a cookie wipe.
        if (Request.Cookies.TryGetValue("refresh_token", out var raw) && !string.IsNullOrEmpty(raw))
        {
            var token = await _refreshRepo.FindByHashAsync(RefreshTokenService.Hash(raw), HttpContext.RequestAborted);
            if (token is not null)
            {
                await _refreshRepo.RevokeFamilyAsync(token.FamilyId, DateTime.UtcNow, HttpContext.RequestAborted);
                await _refreshRepo.SaveChangesAsync(HttpContext.RequestAborted);
            }
        }
        DeleteAuthCookies();
        return NoContent();
    }

    // Any authenticated user may drop all of their own sessions everywhere.
    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpPost("logout-all")]
    public async Task<IActionResult> LogoutAll()
    {
        var userId = User.TryGetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        await _refreshRepo.RevokeAllForUserAsync(userId.Value, DateTime.UtcNow, HttpContext.RequestAborted);
        await _refreshRepo.SaveChangesAsync(HttpContext.RequestAborted);
        DeleteAuthCookies();
        return NoContent();
    }

    // Lists the caller's own active sessions; flags the one whose refresh
    // cookie was presented (available because the cookie is scoped to /api/auth).
    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("sessions")]
    public async Task<IActionResult> Sessions()
    {
        var userId = User.TryGetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        string? currentHash = null;
        if (Request.Cookies.TryGetValue("refresh_token", out var raw) && !string.IsNullOrEmpty(raw))
        {
            currentHash = RefreshTokenService.Hash(raw);
        }

        var active = await _refreshRepo.GetActiveForUserAsync(userId.Value, HttpContext.RequestAborted);
        var sessions = active.Select(t => new SessionDto(
            t.Id,
            t.CreatedAtUtc,
            t.LastUsedAtUtc,
            t.UserAgent,
            t.CreatedByIp,
            currentHash is not null && t.TokenHash == currentHash)).ToList();

        return Ok(sessions);
    }

    // Revokes one of the caller's own sessions by id. A session that isn't the
    // caller's (or doesn't exist) is indistinguishable → 404, so this endpoint
    // can't be used to probe or revoke other users' sessions.
    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpDelete("sessions/{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id)
    {
        var userId = User.TryGetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var active = await _refreshRepo.GetActiveForUserAsync(userId.Value, HttpContext.RequestAborted);
        var target = active.FirstOrDefault(t => t.Id == id);
        if (target is null)
        {
            return NotFound();
        }

        await _refreshRepo.RevokeFamilyAsync(target.FamilyId, DateTime.UtcNow, HttpContext.RequestAborted);
        await _refreshRepo.SaveChangesAsync(HttpContext.RequestAborted);
        return NoContent();
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth-refresh")]
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh()
    {
        var ct = HttpContext.RequestAborted;

        if (!Request.Cookies.TryGetValue("refresh_token", out var raw) || string.IsNullOrEmpty(raw))
        {
            DeleteAuthCookies();
            return Unauthorized();
        }

        var ua = Request.Headers.UserAgent.ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var rotation = await _refreshTokens.RotateAsync(raw, ua, ip, ct);

        if (!rotation.Ok)
        {
            // A presented-but-dead token that maps to a known family is a replay:
            // RotateAsync has already burned the whole family. Record it.
            if (rotation.ReuseDetected)
            {
                await _audit.AddAsync(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    ActorId = rotation.UserId,
                    Action = "auth.refresh_reuse_detected",
                    TargetId = rotation.UserId,
                    OccurredAt = DateTime.UtcNow,
                });
                await _audit.SaveChangesAsync();
            }
            DeleteAuthCookies();
            return Unauthorized();
        }

        var user = await _users.GetByIdAsync(rotation.UserId);
        if (user is null || !user.IsActive)
        {
            // User vanished or was disabled between issuing and refreshing —
            // don't hand back a fresh access token. The rotated refresh row is
            // orphaned but harmless (it can never mint a token now).
            DeleteAuthCookies();
            return Unauthorized();
        }

        var access = _tokenService.CreateUserAuthResponse(user);
        AppendAccessCookie(access.AccessToken);
        // Honor the session's original remember-me choice (carried on the token
        // across rotation): a session-only login stays session-only, a
        // remembered one keeps its persistent Expires. Never silently upgrade a
        // "don't remember me" login into a 30-day on-disk cookie.
        AppendRefreshCookie(rotation.RawToken!, rememberMe: rotation.Persistent, rotation.ExpiresAtUtc);
        return Ok(new { user = access.User });
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var userId = User.TryGetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var result = await _authService.GetCurrentUserAsync(userId.Value);
        return Ok(result);
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.TryGetUserId();
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        // Capture the current session's remember-me choice before the change
        // revokes it, so the re-issued session below preserves it.
        var wasPersistent = false;
        if (Request.Cookies.TryGetValue("refresh_token", out var presented) && !string.IsNullOrEmpty(presented))
        {
            var presentedToken = await _refreshRepo.FindByHashAsync(RefreshTokenService.Hash(presented), HttpContext.RequestAborted);
            wasPersistent = presentedToken?.Persistent ?? false;
        }

        try
        {
            var result = await _authService.ChangePasswordAsync(userId.Value, request);

            // ChangePasswordAsync just revoked every refresh session (including
            // this device's). Mint a brand-new session so the current device
            // stays signed in while all the user's other sessions are dropped.
            var familyId = Guid.NewGuid();
            var refresh = await _refreshTokens.IssueAsync(
                userId.Value,
                familyId,
                Request.Headers.UserAgent.ToString(),
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                HttpContext.RequestAborted,
                persistent: wasPersistent);
            await _refreshRepo.SaveChangesAsync(HttpContext.RequestAborted);

            // Rotate the access cookie too so the browser stops carrying the old
            // JWT (whose security stamp the service just invalidated). Without
            // this the user is involuntarily signed out 30 s later when the
            // stamp cache expires and the validator rejects the stale token.
            // The access cookie is session-scoped; the refresh cookie preserves
            // the session's original remember-me choice.
            AppendAccessCookie(result.AccessToken);
            AppendRefreshCookie(refresh.RawToken, rememberMe: wasPersistent, refresh.ExpiresAtUtc);

            return Ok(new { user = result.User });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // Access cookie: always session-scoped (no Expires). The access token
    // self-expires in minutes; persistence across restarts comes from the
    // refresh cookie, not this one.
    private void AppendAccessCookie(string token)
    {
        Response.Cookies.Append("access_token", token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
    }

    // Refresh cookie is scoped to the /api/auth subtree so it never rides along
    // with ordinary API calls (jobs, agents, artifacts…), yet is still presented
    // to the session-management endpoints under /api/auth (refresh, logout,
    // logout-all, sessions) that need to identify the current session.
    private const string RefreshCookiePath = "/api/auth";

    // Remember-me controls only whether the refresh cookie persists (explicit
    // Expires) or dies with the browser session.
    private void AppendRefreshCookie(string raw, bool rememberMe, DateTime expiresAtUtc)
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
        };
        if (rememberMe)
        {
            opts.Expires = expiresAtUtc;
        }
        Response.Cookies.Append("refresh_token", raw, opts);
    }

    // Cookie.Delete must match the attributes used at Append time, otherwise
    // some browsers silently ignore the deletion and leave a stale token.
    private void DeleteAuthCookies()
    {
        Response.Cookies.Delete("access_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
        Response.Cookies.Delete("refresh_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
        });
    }
}
