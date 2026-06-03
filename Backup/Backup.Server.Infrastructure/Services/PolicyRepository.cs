using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class PolicyRepository : IPolicyRepository
{
    private readonly AppDbContext _dbContext;

    public PolicyRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<List<BackupPolicy>> GetAllPoliciesAsync()
    {
        return await _dbContext.BackupPolicies
            .AsNoTracking()
            .Include(x => x.DatabaseSettings)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<BackupPolicy?> GetPolicyByName(Guid agentId, string name)
    {
        return await _dbContext.BackupPolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.AgentId == agentId && x.Name == name);
    }

    public async Task<List<BackupPolicy>> GetAllPolicies(Guid agentId)
    {
        return await _dbContext.BackupPolicies
            .AsNoTracking()
            .Include(x => x.DatabaseSettings)
            .Where(x => x.AgentId == agentId)
            .ToListAsync();
    }

    public async Task<BackupPolicy?> GetPolicyById(Guid policyId)
    {
        return await _dbContext.BackupPolicies
            .Include(x => x.DatabaseSettings)
            .FirstOrDefaultAsync(x => x.Id == policyId);
    }

    public async Task AddPolicy(BackupPolicy policy)
    {
        await _dbContext.BackupPolicies.AddAsync(policy);
    }

    public async Task UpdatePolicy(BackupPolicy policy)
    {
        _dbContext.Update(policy);
    }

    public async Task DeletePolicy(BackupPolicy policy)
    {
        _dbContext.Remove(policy);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }

    public async Task IncrementFailureStreakAsync(Guid policyId, string? lastFailureReason)
    {
        // Set-based atomic update: the counter is incremented relative to its
        // current DB value, so two concurrent failures both land (no lost
        // update) without needing a row lock or optimistic-concurrency token.
        await _dbContext.BackupPolicies
            .Where(p => p.Id == policyId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ConsecutiveFailureCount, p => p.ConsecutiveFailureCount + 1)
                .SetProperty(p => p.LastFailureReason, lastFailureReason));
    }

    public async Task<bool> TryAutoDisableAsync(Guid policyId, int threshold, DateTime nowUtc)
    {
        // The WHERE clause makes the transition single-shot: once the first
        // caller flips IsEnabled to false the predicate no longer matches, so
        // later racers update zero rows and return false.
        var affected = await _dbContext.BackupPolicies
            .Where(p => p.Id == policyId && p.IsEnabled && p.ConsecutiveFailureCount >= threshold)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.IsEnabled, false)
                .SetProperty(p => p.AutoDisabledAt, nowUtc));

        return affected > 0;
    }
}
