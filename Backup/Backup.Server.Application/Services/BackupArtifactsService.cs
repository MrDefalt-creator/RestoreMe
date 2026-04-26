using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Services;

public class BackupArtifactsService
{
    private readonly IBackupArtifactRepository _backupArtifactRepository;
    private readonly IStorageAccessService _storageAccessService;

    public BackupArtifactsService(
        IBackupArtifactRepository backupArtifactRepository,
        IStorageAccessService storageAccessService)
    {
        _backupArtifactRepository = backupArtifactRepository;
        _storageAccessService = storageAccessService;
    }

    public async Task<List<BackupArtifact>> GetAllArtifacts()
    {
        return await _backupArtifactRepository.GetAllArtifactsAsync();
    }

    public async Task<List<BackupArtifact>> GetArtifactsByJobId(Guid jobId)
    {
        return await _backupArtifactRepository.GetArtifactsByJobIdAsync(jobId);
    }

    public async Task<ArtifactDownloadResult> DownloadArtifact(Guid artifactId, CancellationToken cancellationToken)
    {
        var artifact = await _backupArtifactRepository.GetArtifactByIdAsync(artifactId);
        if (artifact == null)
        {
            throw new ApplicationException($"Artifact with id {artifactId} does not exist");
        }

        var stream = await _storageAccessService.OpenDownloadStreamAsync(
            artifact.ObjectKey,
            cancellationToken);

        return new ArtifactDownloadResult(stream, artifact.FileName, "application/octet-stream");
    }
}

public sealed record ArtifactDownloadResult(
    Stream Content,
    string FileName,
    string ContentType);
