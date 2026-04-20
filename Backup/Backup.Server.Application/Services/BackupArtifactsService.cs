using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Services;

public class BackupArtifactsService
{
    private readonly IBackupArtifactRepository _backupArtifactRepository;

    public BackupArtifactsService(IBackupArtifactRepository backupArtifactRepository)
    {
        _backupArtifactRepository = backupArtifactRepository;
    }

    public async Task<List<BackupArtifact>> GetAllArtifacts()
    {
        return await _backupArtifactRepository.GetAllArtifactsAsync();
    }

    public async Task<List<BackupArtifact>> GetArtifactsByJobId(Guid jobId)
    {
        return await _backupArtifactRepository.GetArtifactsByJobIdAsync(jobId);
    }
}
