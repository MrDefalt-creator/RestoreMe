using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
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

    [HttpGet]
    public async Task<IActionResult> GetJobs()
    {
        var jobs = await _service.GetAllJobs();
        return Ok(jobs.Select(MapJob));
    }

    [HttpGet("{jobId:guid}")]
    public async Task<IActionResult> GetJob([FromRoute] Guid jobId)
    {
        var job = await _service.GetJobById(jobId);
        return Ok(MapJob(job));
    }
    
    [HttpGet("agent/{agentId:guid}")]
    public async Task<IActionResult> GetJobsByAgent([FromRoute] Guid agentId)
    {
        var jobs = await _service.GetJobsByAgentId(agentId);
        return Ok(jobs.Select(MapJob));
    }
    
    [HttpGet("policy/{policyId:guid}")]
    public async Task<IActionResult> GetJobsByPolicy([FromRoute] Guid policyId)
    {
        var jobs = await _service.GetJobsByPolicyId(policyId);
        return Ok(jobs.Select(MapJob));
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

    private static AdminBackupJobDto MapJob(BackupJob job)
    {
        return new AdminBackupJobDto(
            job.Id,
            job.AgentId,
            job.PolicyId,
            job.Status.ToString().ToLowerInvariant(),
            job.StartedAt,
            job.CompletedAt,
            job.ErrorMessage);
    }
}
