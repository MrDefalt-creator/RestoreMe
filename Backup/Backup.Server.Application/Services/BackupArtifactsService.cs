using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Options;
using Backup.Shared.Contracts.DTOs.Artifacts;
using Microsoft.Extensions.Options;

namespace Backup.Server.Application.Services;

public class BackupArtifactsService
{
    private readonly IBackupArtifactRepository _backupArtifactRepository;
    private readonly IStorageAccessService _storageAccessService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly StorageOptions _storageOptions;

    public BackupArtifactsService(
        IBackupArtifactRepository backupArtifactRepository,
        IStorageAccessService storageAccessService,
        IAuditLogRepository auditLogRepository,
        IOptions<StorageOptions> storageOptions)
    {
        _backupArtifactRepository = backupArtifactRepository;
        _storageAccessService = storageAccessService;
        _auditLogRepository = auditLogRepository;
        _storageOptions = storageOptions.Value;
    }

    public async Task<List<BackupArtifact>> GetAllArtifacts()
    {
        return await _backupArtifactRepository.GetAllArtifactsAsync();
    }

    public async Task<PagedResult<BackupArtifact>> QueryArtifacts(PagedQuery query, CancellationToken cancellationToken)
    {
        return await _backupArtifactRepository.QueryArtifactsAsync(query, cancellationToken);
    }

    public async Task<List<BackupArtifact>> GetArtifactsByJobId(Guid jobId)
    {
        return await _backupArtifactRepository.GetArtifactsByJobIdAsync(jobId);
    }

    public async Task<BackupArtifact> GetArtifactForDownloadAsync(Guid artifactId)
    {
        var artifact = await _backupArtifactRepository.GetArtifactByIdAsync(artifactId);
        if (artifact == null)
        {
            throw new KeyNotFoundException($"Artifact with id {artifactId} does not exist");
        }

        return artifact;
    }

    public Task StreamArtifactToAsync(
        BackupArtifact artifact,
        Stream destination,
        CancellationToken cancellationToken)
    {
        return _storageAccessService.WriteObjectToAsync(
            artifact.ObjectKey,
            destination,
            cancellationToken);
    }

    public async Task<ArtifactVerifyResultDto> VerifyArtifactAsync(Guid artifactId, Guid? actorId, CancellationToken cancellationToken)
    {
        var artifact = await _backupArtifactRepository.GetArtifactByIdAsync(artifactId)
            ?? throw new KeyNotFoundException($"Artifact with id {artifactId} does not exist");

        if (_storageOptions.ChecksumVerifyMaxBytes is not null
            && artifact.SizeBytes > _storageOptions.ChecksumVerifyMaxBytes.Value)
        {
            await _auditLogRepository.AddAsync(Audit("artifact.verify_skipped", actorId, artifact.Id,
                $"objectKey={artifact.ObjectKey} size={artifact.SizeBytes} limit={_storageOptions.ChecksumVerifyMaxBytes.Value}"));
            await _auditLogRepository.SaveChangesAsync();
            return new ArtifactVerifyResultDto(artifact.Id, artifact.IntegrityStatus.ToString(), artifact.LastVerifiedAt);
        }

        var computed = await _storageAccessService.ComputeObjectSha256Async(artifact.ObjectKey, cancellationToken);
        var match = string.Equals(computed, artifact.Checksum, StringComparison.OrdinalIgnoreCase);
        var status = match ? ArtifactIntegrityStatus.Verified : ArtifactIntegrityStatus.Failed;
        var verifiedAt = match ? (DateTime?)DateTime.UtcNow : null;

        await _backupArtifactRepository.UpdateIntegrityAsync(artifact.Id, status, verifiedAt, cancellationToken);
        await _auditLogRepository.AddAsync(Audit("artifact.verify_manual", actorId, artifact.Id,
            match ? $"objectKey={artifact.ObjectKey} result=verified"
                  : $"objectKey={artifact.ObjectKey} result=failed expected={artifact.Checksum} actual={computed}"));
        await _auditLogRepository.SaveChangesAsync();

        return new ArtifactVerifyResultDto(artifact.Id, status.ToString(), verifiedAt);
    }

    private static AuditLog Audit(string action, Guid? actorId, Guid targetId, string details) => new()
    {
        Id = Guid.NewGuid(),
        ActorId = actorId,
        Action = action,
        TargetId = targetId,
        Details = details,
        OccurredAt = DateTime.UtcNow,
    };
}
