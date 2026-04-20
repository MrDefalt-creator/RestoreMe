using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IBackupJobRepository
{
    public Task<List<BackupJob>> GetAllBackupJobsAsync();
    
    public Task<List<BackupJob>> GetBackupJobsByAgentIdAsync(Guid agentId);
    
    public Task<List<BackupJob>> GetBackupJobsByPolicyIdAsync(Guid policyId);
    
    public Task AddBackupJob(BackupJob job);
    
    public Task SaveChangesAsync();
    
    public Task<BackupJob?> GetBackupJob(Guid jobId);
    
    public Task UpdateBackupJob(BackupJob job);
}
