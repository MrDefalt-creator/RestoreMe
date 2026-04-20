using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Services;

public class PoliciesService
{
    private readonly IPolicyRepository _policyRepository;
    public PoliciesService(IPolicyRepository policyRepository)
    {
        _policyRepository = policyRepository;
    }

    public async Task<BackupPolicy> CreatePolicy(Guid agentId, string name, string sourcePath, int interval)
    {
        var policy = await _policyRepository.GetPolicyByName(agentId, name);

        if (policy != null)
        {
            throw new Exception("Policy already exists");
        }
        
        sourcePath = NormalizePath(sourcePath);

        policy = new BackupPolicy
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            Name = name,
            SourcePath = sourcePath.Replace("\\", "/").Trim(),
            IntervalSeconds =  interval,
            NextRunAt = DateTime.UtcNow
        };
        
        await _policyRepository.AddPolicy(policy);
        
        await _policyRepository.SaveChangesAsync();

        return policy;
    }

    public async Task<List<BackupPolicy>> GetAllPolicies(Guid agentId)
    {
        var policies = await _policyRepository.GetAllPolicies(agentId);
        
        return policies;
    }
    
    public async Task<List<BackupPolicy>> GetAllPolicies()
    {
        return await _policyRepository.GetAllPoliciesAsync();
    }

    public async Task<BackupPolicy> GetPolicyById(Guid policyId)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);

        if (policy == null)
        {
            throw new Exception("Policy not found");
        }
        
        return policy;
    }
    
    public async Task<BackupPolicy> UpdatePolicy(Guid policyId, Guid agentId, string name, string sourcePath, int intervalSeconds, bool isEnabled)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);
        if (policy == null)
        {
            throw new Exception("Policy not found");
        }
        
        sourcePath = NormalizePath(sourcePath);
        
        policy.AgentId = agentId;
        policy.Name = name.Trim();
        policy.SourcePath = sourcePath.Replace("\\", "/").Trim();
        policy.IntervalSeconds = intervalSeconds;
        policy.IsEnabled = isEnabled;
        policy.NextRunAt = DateTime.UtcNow.AddSeconds(intervalSeconds);
        
        await _policyRepository.UpdatePolicy(policy);
        await _policyRepository.SaveChangesAsync();

        return policy;
    }
    
    public async Task<BackupPolicy> TogglePolicy(Guid policyId)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);
        if (policy == null)
        {
            throw new Exception("Policy not found");
        }
        
        policy.IsEnabled = !policy.IsEnabled;
        
        await _policyRepository.UpdatePolicy(policy);
        await _policyRepository.SaveChangesAsync();

        return policy;
    }
    
    public async Task MarkPolicyExecuted(Guid policyId)
    {
        var policy = await _policyRepository.GetPolicyById(policyId);

        if (policy == null)
        {
            throw new Exception("Policy not found");
        }
        
        policy.LastRunAt = DateTime.UtcNow;
        policy.NextRunAt = DateTime.UtcNow.AddSeconds(policy.IntervalSeconds);
        await _policyRepository.UpdatePolicy(policy);
        await _policyRepository.SaveChangesAsync();
    }
    
    private static string NormalizePath(string path)
    {
        path = path.Trim();
        path = path.Replace('\\', '/');

        while (path.Contains("//"))
            path = path.Replace("//", "/");

        return path;
    }
}
