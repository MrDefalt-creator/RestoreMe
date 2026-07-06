using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Application.Interfaces;

public interface IBackupJobRepository
{
    public Task<List<BackupJob>> GetAllBackupJobsAsync();

    // Sort keys: startedAt (default), completedAt, status. Optional status
    // filter narrows both the items and the reported total.
    public Task<PagedResult<BackupJob>> QueryBackupJobsAsync(PagedQuery query, BackupJobStatus? status, CancellationToken cancellationToken);
    
    public Task<List<BackupJob>> GetBackupJobsByAgentIdAsync(Guid agentId);
    
    public Task<List<BackupJob>> GetBackupJobsByPolicyIdAsync(Guid policyId);
    
    public Task AddBackupJob(BackupJob job);
    
    public Task SaveChangesAsync();
    
    public Task<BackupJob?> GetBackupJob(Guid jobId);

    public Task UpdateBackupJob(BackupJob job);

    public Task ExecuteInTransactionAsync(Func<Task> action);
}
