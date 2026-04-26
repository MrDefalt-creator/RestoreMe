using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Artifacts;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
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
    public async Task<IActionResult> DownloadArtifact([FromRoute] Guid artifactId, CancellationToken cancellationToken)
    {
        var artifact = await _backupArtifactsService.DownloadArtifact(artifactId, cancellationToken);
        return File(artifact.Content, artifact.ContentType, artifact.FileName);
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
