using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class BackupJobRepository : IBackupJobRepository
{
    private readonly AppDbContext _dbContext;
    public BackupJobRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<BackupJob>> GetAllBackupJobsAsync()
    {
        return await _dbContext.BackupJobs
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();
    }
    
    public async Task<List<BackupJob>> GetBackupJobsByAgentIdAsync(Guid agentId)
    {
        return await _dbContext.BackupJobs
            .Where(x => x.AgentId == agentId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();
    }
    
    public async Task<List<BackupJob>> GetBackupJobsByPolicyIdAsync(Guid policyId)
    {
        return await _dbContext.BackupJobs
            .Where(x => x.PolicyId == policyId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();
    }
    
    public async Task AddBackupJob(BackupJob job)
    {
        await _dbContext.BackupJobs.AddAsync(job);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }

    public async Task<BackupJob?> GetBackupJob(Guid jobId)
    {
        return await _dbContext.BackupJobs.FirstOrDefaultAsync(x => x.Id == jobId);
    }

    public async Task UpdateBackupJob(BackupJob job)
    {
        _dbContext.BackupJobs.Update(job);
    }
}
