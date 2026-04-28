using Backup.Server.Api.Security;
using Backup.Server.Api.Services;
using Backup.Shared.Contracts.DTOs.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthConstants.UserManagementPolicy)]
public class UsersController : ControllerBase
{
    private readonly UsersService _usersService;

    public UsersController(UsersService usersService)
    {
        _usersService = usersService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        return Ok(await _usersService.GetUsersAsync());
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            return Ok(await _usersService.CreateUserAsync(request));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{userId:guid}/role")]
    public async Task<IActionResult> UpdateRole([FromRoute] Guid userId, [FromBody] UpdateUserRoleRequest request)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _usersService.UpdateRoleAsync(actorUserId.Value, userId, request.Role));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{userId:guid}/status")]
    public async Task<IActionResult> UpdateStatus([FromRoute] Guid userId, [FromBody] UpdateUserStatusRequest request)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _usersService.UpdateStatusAsync(actorUserId.Value, userId, request.IsActive));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPatch("{userId:guid}/password")]
    public async Task<IActionResult> SetPassword([FromRoute] Guid userId, [FromBody] SetUserPasswordRequest request)
    {
        await _usersService.SetPasswordAsync(userId, request.NewPassword);
        return NoContent();
    }

    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> DeleteUser([FromRoute] Guid userId)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            await _usersService.DeleteUserAsync(actorUserId.Value, userId);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
