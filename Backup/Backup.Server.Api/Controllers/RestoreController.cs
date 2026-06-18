using Backup.Server.Api.Security;
using Backup.Server.Application.Services;
using Backup.Shared.Contracts.DTOs.Restore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RestoreController : ControllerBase
{
    private readonly RestoreJobsService _service;

    public RestoreController(RestoreJobsService service)
    {
        _service = service;
    }

    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    [HttpPost]
    public async Task<IActionResult> CreateRestore([FromBody] CreateRestoreRequest request)
    {
        try
        {
            var restoreJobId = await _service.CreateRestoreAsync(request, HttpContext.RequestAborted);
            return Ok(new { restoreJobId });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("{restoreJobId:guid}/status")]
    public async Task<IActionResult> GetStatus([FromRoute] Guid restoreJobId)
    {
        try
        {
            var status = await _service.GetStatusAsync(restoreJobId, HttpContext.RequestAborted);
            return Ok(status);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var agentId = User.TryGetAgentId();
        if (!agentId.HasValue) return Forbid();

        var pending = await _service.GetPendingForAgentAsync(agentId.Value);
        if (pending is null) return NoContent();

        return Ok(pending);
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [EnableRateLimiting("agent-write")]
    [HttpPost("download_ticket/{jobId:guid}")]
    public async Task<IActionResult> RequestDownloadTicket([FromRoute] Guid jobId)
    {
        var agentId = User.TryGetAgentId();
        if (!agentId.HasValue) return Forbid();

        try
        {
            var publicServerBaseUrl = $"{Request.Scheme}://{Request.Host}";
            var downloadUrl = await _service.GetDownloadTicketAsync(
                jobId,
                agentId.Value,
                publicServerBaseUrl,
                HttpContext.RequestAborted);
            return Ok(new DownloadTicketResponse(downloadUrl));
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [EnableRateLimiting("agent-write")]
    [HttpPost("complete/{jobId:guid}")]
    public async Task<IActionResult> Complete([FromRoute] Guid jobId)
    {
        var agentId = User.TryGetAgentId();
        if (!agentId.HasValue) return Forbid();

        try
        {
            await _service.CompleteAsync(jobId, agentId.Value);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [EnableRateLimiting("agent-write")]
    [HttpPost("failed")]
    public async Task<IActionResult> Failed([FromBody] FailRestoreJobRequest request)
    {
        var agentId = User.TryGetAgentId();
        if (!agentId.HasValue) return Forbid();

        try
        {
            await _service.FailedAsync(request.JobId, agentId.Value, request.ErrorMessage);
            return Ok();
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
    }
}
