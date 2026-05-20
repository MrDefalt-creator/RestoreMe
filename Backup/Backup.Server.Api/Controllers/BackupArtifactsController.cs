using Backup.Server.Api.Security;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Artifacts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = AuthConstants.AdminReadPolicy)]
public class BackupArtifactsController : ControllerBase
{
    private readonly BackupArtifactsService _backupArtifactsService;

    public BackupArtifactsController(BackupArtifactsService backupArtifactsService)
    {
        _backupArtifactsService = backupArtifactsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetArtifacts()
    {
        var artifacts = await _backupArtifactsService.GetAllArtifacts();
        return Ok(artifacts.Select(MapArtifact));
    }

    [HttpGet("job/{jobId:guid}")]
    public async Task<IActionResult> GetArtifactsByJob([FromRoute] Guid jobId)
    {
        var artifacts = await _backupArtifactsService.GetArtifactsByJobId(jobId);
        return Ok(artifacts.Select(MapArtifact));
    }

    [HttpGet("{artifactId:guid}/download")]
    public async Task DownloadArtifact([FromRoute] Guid artifactId, CancellationToken cancellationToken)
    {
        var artifact = await _backupArtifactsService.GetArtifactForDownloadAsync(artifactId);

        Response.ContentType = "application/octet-stream";
        Response.Headers.ContentDisposition =
            $"attachment; filename=\"{Uri.EscapeDataString(artifact.FileName)}\"";
        if (artifact.SizeBytes > 0)
        {
            Response.ContentLength = artifact.SizeBytes;
        }

        await _backupArtifactsService.StreamArtifactToAsync(
            artifact,
            Response.Body,
            cancellationToken);
    }

    private static BackupArtifactDto MapArtifact(BackupArtifact artifact)
    {
        return new BackupArtifactDto(
            artifact.Id,
            artifact.JobId,
            artifact.FileName,
            artifact.ObjectKey,
            artifact.SizeBytes,
            artifact.Checksum,
            artifact.CreatedAt);
    }
}
