using Backup.Server.Application.Services;
using Backup.Shared.Contracts.DTOs;
using Backup.Shared.Contracts.DTOs.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupJobsController : ControllerBase
{
    private readonly BackupJobsService _service;
    public BackupJobsController(BackupJobsService service)
    {
        _service = service;
    }
    
    [HttpPost("start")]
    public async Task<IActionResult> StartJob([FromBody] StartBackupJobRequest request)
    {
        var jobId = await _service.Start(request.AgentId, request.PolicyId);
        return Ok(new StartBackupJobResponse(jobId));
    }
    
    [HttpPost("complete/{jobId}")]
    public async Task<IActionResult> CompletedJob([FromRoute] Guid jobId)
    {
        await _service.Complete(jobId);
        return Ok();
    }
    
    [HttpPost ("failed")]
    public async Task<IActionResult> FailedJob([FromBody] FailedBackupJobRequest request)
    {
        await _service.Failed(request.JobId, request.ErrorMessage);
        return Ok();
    }
    
    [HttpPost("add_artifact")]
    public async Task<IActionResult> AddArtifact([FromBody] AddArtifactBackupJobRequest request)
    {
        await _service.AddArtifact(request.JobId, request.FileName, request.ObjectKey, request.Size, request.Сhecksum);
        return Ok();
    }
    
    [HttpPost("upload_ticket")]
    public async Task<IActionResult> RequestUploadTicket([FromBody] RequestUploadTicketRequest request)
    {
        var response = await _service.RequestUploadTicketAsync(request);
        return Ok(response);
    }
}