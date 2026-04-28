using Backup.Server.Api.Security;
using Backup.Server.Api.Services;
using Backup.Shared.Contracts.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            var result = await _authService.LoginAsync(request.Username, request.Password);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
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
            await _authService.ChangePasswordAsync(userId.Value, request);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}
