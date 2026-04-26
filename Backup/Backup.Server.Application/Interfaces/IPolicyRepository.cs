using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IPolicyRepository
{
    public Task<List<BackupPolicy>> GetAllPoliciesAsync();
    public Task<BackupPolicy?> GetPolicyByName(Guid agentId, string name);
    
    public Task<List<BackupPolicy>> GetAllPolicies(Guid agentId);
    
    public Task<BackupPolicy?> GetPolicyById(Guid policyId);
    
    public Task AddPolicy(BackupPolicy policy);
    
    public Task UpdatePolicy(BackupPolicy policy);
    
    public Task DeletePolicy(BackupPolicy policy);
    
    public Task SaveChangesAsync();
    
    
}
