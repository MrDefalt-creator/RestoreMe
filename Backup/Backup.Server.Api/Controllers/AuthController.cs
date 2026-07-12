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
                HttpContext.RequestAborted);
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
    public IActionResult Logout()
    {
        DeleteAuthCookies();
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
        // Persist the rotated refresh cookie unconditionally: the server row
        // carries the absolute lifetime cap, so always stamping Expires keeps a
        // remembered session alive across rotations without threading the
        // original remember-me flag through every hop.
        AppendRefreshCookie(rotation.RawToken!, rememberMe: true, rotation.ExpiresAtUtc);
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

        try
        {
            var result = await _authService.ChangePasswordAsync(userId.Value, request);

            // Rotate the auth cookie so the browser stops carrying the old
            // JWT (whose security stamp the service just invalidated). Without
            // this the user is involuntarily signed out 30 s later when the
            // stamp cache expires and the validator rejects the stale token.
            // We mint a session cookie here — the previous "Remember me"
            // preference isn't recoverable mid-session, and downgrading to a
            // session cookie is the safer default after a password rotation.
            AppendAccessCookie(result.AccessToken);

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

    // Refresh cookie: scoped to the refresh endpoint so it never rides along
    // with ordinary API calls. Remember-me controls only whether it persists
    // (explicit Expires) or dies with the browser session.
    private void AppendRefreshCookie(string raw, bool rememberMe, DateTime expiresAtUtc)
    {
        var opts = new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/api/auth/refresh",
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
            Path = "/api/auth/refresh",
        });
    }
}
