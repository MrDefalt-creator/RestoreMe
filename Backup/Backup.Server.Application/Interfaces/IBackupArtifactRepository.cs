using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IBackupArtifactRepository
{
    public Task<List<BackupArtifact>> GetAllArtifactsAsync();
    
    public Task<List<BackupArtifact>> GetArtifactsByJobIdAsync(Guid jobId);

    public Task<BackupArtifact?> GetArtifactByIdAsync(Guid artifactId);

    public Task<int> CountByJobIdAsync(Guid jobId);

    public Task AddArtifact(BackupArtifact artifact);

    public Task<List<BackupArtifact>> GetExpiredArtifactsAsync(CancellationToken cancellationToken);

    public Task DeleteArtifactAsync(Guid id, CancellationToken cancellationToken);

    public Task SaveChanges();
}
