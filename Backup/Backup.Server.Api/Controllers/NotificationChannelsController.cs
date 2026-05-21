using Backup.Server.Api.Security;
using Backup.Server.Application.Services;
using Backup.Shared.Contracts.DTOs.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/notification-channels")]
[Authorize(Policy = AuthConstants.UserManagementPolicy)]
public class NotificationChannelsController : ControllerBase
{
    private readonly NotificationChannelsService _service;

    public NotificationChannelsController(NotificationChannelsService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var channels = await _service.ListAsync(cancellationToken);
        return Ok(channels);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNotificationChannelRequest request, CancellationToken cancellationToken)
    {
        var channel = await _service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(List), new { id = channel.Id }, channel);
    }

    [HttpPut("{channelId:guid}")]
    public async Task<IActionResult> Update(
        [FromRoute] Guid channelId,
        [FromBody] UpdateNotificationChannelRequest request,
        CancellationToken cancellationToken)
    {
        var channel = await _service.UpdateAsync(channelId, request, cancellationToken);
        return Ok(channel);
    }

    [HttpDelete("{channelId:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid channelId, CancellationToken cancellationToken)
    {
        await _service.DeleteAsync(channelId, cancellationToken);
        return NoContent();
    }

    [HttpPost("{channelId:guid}/test")]
    public async Task<IActionResult> Test([FromRoute] Guid channelId, CancellationToken cancellationToken)
    {
        var actorId = User.TryGetUserId();
        var result = await _service.TestAsync(channelId, actorId, cancellationToken);
        return Ok(result);
    }
}
