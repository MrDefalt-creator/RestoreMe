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
}
