using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IBackupJobRepository
{
    public Task AddBackupJob(BackupJob job);
    
    public Task SaveChangesAsync();
    
    public Task<BackupJob?> GetBackupJob(Guid jobId);
    
    public Task UpdateBackupJob(BackupJob job);
}