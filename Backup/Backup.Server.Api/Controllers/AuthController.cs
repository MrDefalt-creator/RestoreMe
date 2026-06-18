using Backup.Server.Api.Security;
using Backup.Server.Api.Services;
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
    private readonly IWebHostEnvironment _env;

    public AuthController(AuthService authService, IWebHostEnvironment env)
    {
        _authService = authService;
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

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = !_env.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Path = "/",
            };

            if (request.RememberMe)
            {
                cookieOptions.Expires = result.ExpiresAtUtc;
            }

            Response.Cookies.Append("access_token", result.AccessToken, cookieOptions);

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
        // Cookie.Delete must match the attributes used at Append time, otherwise
        // some browsers silently ignore the deletion and leave a stale token.
        Response.Cookies.Delete("access_token", new CookieOptions
        {
            HttpOnly = true,
            Secure = !_env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Path = "/",
        });
        return NoContent();
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
            Response.Cookies.Append("access_token", result.AccessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = !_env.IsDevelopment(),
                SameSite = SameSiteMode.Strict,
                Path = "/",
            });

            return Ok(new { user = result.User });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}
