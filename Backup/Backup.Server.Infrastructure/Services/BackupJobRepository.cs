using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
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
            .AsNoTracking()
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();
    }

    public async Task<PagedResult<BackupJob>> QueryBackupJobsAsync(PagedQuery query, BackupJobStatus? status, CancellationToken cancellationToken)
    {
        var jobs = _dbContext.BackupJobs.AsNoTracking();
        if (status.HasValue)
        {
            jobs = jobs.Where(x => x.Status == status.Value);
        }

        var ordered = query.SortBy?.ToLowerInvariant() switch
        {
            "completedat" => query.SortDescending
                ? jobs.OrderByDescending(x => x.CompletedAt)
                : jobs.OrderBy(x => x.CompletedAt),
            "status" => (query.SortDescending
                    ? jobs.OrderByDescending(x => x.Status)
                    : jobs.OrderBy(x => x.Status))
                .ThenByDescending(x => x.StartedAt),
            _ => query.SortDescending
                ? jobs.OrderByDescending(x => x.StartedAt)
                : jobs.OrderBy(x => x.StartedAt),
        };

        var total = await jobs.CountAsync(cancellationToken);
        var items = await ordered
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<BackupJob>(items, total);
    }
    
    public async Task<List<BackupJob>> GetBackupJobsByAgentIdAsync(Guid agentId)
    {
        return await _dbContext.BackupJobs
            .AsNoTracking()
            .Where(x => x.AgentId == agentId)
            .OrderByDescending(x => x.StartedAt)
            .ToListAsync();
    }
    
    public async Task<List<BackupJob>> GetBackupJobsByPolicyIdAsync(Guid policyId)
    {
        return await _dbContext.BackupJobs
            .AsNoTracking()
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

    public async Task ExecuteInTransactionAsync(Func<Task> action)
    {
        // EF Core execution strategy wraps the action in a retry loop and
        // owns the transaction lifetime — so retried operations on transient
        // failures still commit atomically.
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _dbContext.Database.BeginTransactionAsync();
            await action();
            await tx.CommitAsync();
        });
    }
}
