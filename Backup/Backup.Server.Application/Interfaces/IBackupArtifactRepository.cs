using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IBackupArtifactRepository
{
    public Task<List<BackupArtifact>> GetAllArtifactsAsync();
    
    public Task<List<BackupArtifact>> GetArtifactsByJobIdAsync(Guid jobId);
    
    public Task AddArtifact(BackupArtifact artifact);
    
    public Task SaveChanges();
}
