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
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<BackupPolicy?> GetPolicyByName(Guid agentId, string name)
    {
        return await _dbContext.BackupPolicies
            .FirstOrDefaultAsync(x => x.AgentId == agentId && x.Name == name);
    }

    public async Task<List<BackupPolicy>> GetAllPolicies(Guid agentId)
    {
        return await _dbContext.BackupPolicies
            .Where(x => x.AgentId == agentId)
            .ToListAsync();
    }

    public async Task<BackupPolicy?> GetPolicyById(Guid policyId)
    {
        return await _dbContext.BackupPolicies
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
}
