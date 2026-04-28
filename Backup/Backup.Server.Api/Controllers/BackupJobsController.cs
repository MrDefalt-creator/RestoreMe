using Backup.Server.Api.Security;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BackupJobsController : ControllerBase
{
    private readonly BackupJobsService _service;
    private readonly PoliciesService _policiesService;

    public BackupJobsController(BackupJobsService service, PoliciesService policiesService)
    {
        _service = service;
        _policiesService = policiesService;
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet]
    public async Task<IActionResult> GetJobs()
    {
        var jobs = await _service.GetAllJobs();
        return Ok(jobs.Select(MapJob));
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("{jobId:guid}")]
    public async Task<IActionResult> GetJob([FromRoute] Guid jobId)
    {
        var job = await _service.GetJobById(jobId);
        return Ok(MapJob(job));
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("agent/{agentId:guid}")]
    public async Task<IActionResult> GetJobsByAgent([FromRoute] Guid agentId)
    {
        var jobs = await _service.GetJobsByAgentId(agentId);
        return Ok(jobs.Select(MapJob));
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("policy/{policyId:guid}")]
    public async Task<IActionResult> GetJobsByPolicy([FromRoute] Guid policyId)
    {
        var jobs = await _service.GetJobsByPolicyId(policyId);
        return Ok(jobs.Select(MapJob));
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [HttpPost("start")]
    public async Task<IActionResult> StartJob([FromBody] StartBackupJobRequest request)
    {
        var currentAgentId = User.TryGetAgentId();
        if (currentAgentId != request.AgentId)
        {
            return Forbid();
        }

        var policy = await _policiesService.GetPolicyById(request.PolicyId);
        if (policy.AgentId != currentAgentId)
        {
            return Forbid();
        }

        var jobId = await _service.Start(request.AgentId, request.PolicyId);
        return Ok(new StartBackupJobResponse(jobId));
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [HttpPost("complete/{jobId:guid}")]
    public async Task<IActionResult> CompletedJob([FromRoute] Guid jobId)
    {
        if (!await AgentOwnsJobAsync(jobId))
        {
            return Forbid();
        }

        await _service.Complete(jobId);
        return Ok();
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [HttpPost("failed")]
    public async Task<IActionResult> FailedJob([FromBody] FailedBackupJobRequest request)
    {
        if (!await AgentOwnsJobAsync(request.JobId))
        {
            return Forbid();
        }

        await _service.Failed(request.JobId, request.ErrorMessage);
        return Ok();
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [HttpPost("add_artifact")]
    public async Task<IActionResult> AddArtifact([FromBody] AddArtifactBackupJobRequest request)
    {
        if (!await AgentOwnsJobAsync(request.JobId))
        {
            return Forbid();
        }

        await _service.AddArtifact(request.JobId, request.FileName, request.ObjectKey, request.Size, request.Checksum);
        return Ok();
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [HttpPost("upload_ticket")]
    public async Task<IActionResult> RequestUploadTicket([FromBody] RequestUploadTicketRequest request)
    {
        var currentAgentId = User.TryGetAgentId();
        if (!currentAgentId.HasValue)
        {
            return Forbid();
        }

        var job = await _service.GetJobById(request.BackupJobId);
        var policy = await _policiesService.GetPolicyById(request.PolicyId);
        if (job.AgentId != currentAgentId.Value || policy.AgentId != currentAgentId.Value)
        {
            return Forbid();
        }

        var publicServerBaseUrl = $"{Request.Scheme}://{Request.Host}";
        var response = await _service.RequestUploadTicketAsync(request, publicServerBaseUrl);
        return Ok(response);
    }

    private async Task<bool> AgentOwnsJobAsync(Guid jobId)
    {
        var currentAgentId = User.TryGetAgentId();
        if (!currentAgentId.HasValue)
        {
            return false;
        }

        var job = await _service.GetJobById(jobId);
        return job.AgentId == currentAgentId.Value;
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
