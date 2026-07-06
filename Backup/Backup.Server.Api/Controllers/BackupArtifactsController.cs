using Backup.Server.Api.Security;
using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Artifacts;
using Backup.Shared.Contracts.DTOs.Common;
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
    public async Task<IActionResult> GetArtifacts(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        CancellationToken cancellationToken)
    {
        // No pagination params → legacy full-array shape, so older clients
        // (and internal callers) keep working unchanged.
        if (page is null && pageSize is null && sortBy is null)
        {
            var artifacts = await _backupArtifactsService.GetAllArtifacts();
            return Ok(artifacts.Select(MapArtifact));
        }

        var query = PagedQuery.Normalize(page, pageSize, sortBy, sortDir);
        var result = await _backupArtifactsService.QueryArtifacts(query, cancellationToken);
        return Ok(new PagedResponse<BackupArtifactDto>(
            result.Items.Select(MapArtifact).ToList(),
            result.Total,
            query.Page,
            query.PageSize));
    }

    [HttpGet("job/{jobId:guid}")]
    public async Task<IActionResult> GetArtifactsByJob([FromRoute] Guid jobId)
    {
        var artifacts = await _backupArtifactsService.GetArtifactsByJobId(jobId);
        return Ok(artifacts.Select(MapArtifact));
    }

    [HttpPost("{artifactId:guid}/verify")]
    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    public async Task<IActionResult> VerifyArtifact([FromRoute] Guid artifactId, CancellationToken cancellationToken)
    {
        var actorId = User.TryGetUserId();
        var result = await _backupArtifactsService.VerifyArtifactAsync(artifactId, actorId, cancellationToken);
        return Ok(result);
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
            artifact.CreatedAt,
            artifact.IntegrityStatus.ToString(),
            artifact.LastVerifiedAt);
    }
}
