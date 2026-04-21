using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IBackupArtifactRepository
{
    public Task<List<BackupArtifact>> GetAllArtifactsAsync();
    
    public Task<List<BackupArtifact>> GetArtifactsByJobIdAsync(Guid jobId);

    public Task<BackupArtifact?> GetArtifactByIdAsync(Guid artifactId);
    
    public Task AddArtifact(BackupArtifact artifact);
    
    public Task SaveChanges();
}
