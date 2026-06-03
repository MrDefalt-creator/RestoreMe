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

    /// <summary>
    /// Atomically increments the consecutive-failure counter and records the
    /// latest failure reason in a single DB statement (no read-modify-write),
    /// so concurrent failure reports for the same policy can't lose an
    /// increment. No-op if the policy no longer exists.
    /// </summary>
    public Task IncrementFailureStreakAsync(Guid policyId, string? lastFailureReason);

    /// <summary>
    /// Atomically flips the policy off *only if* it is still enabled and has
    /// reached <paramref name="threshold"/> consecutive failures. Returns true
    /// for the single caller that performed the transition, false otherwise —
    /// so the auto-disable audit + notification fire exactly once even when
    /// several failures race.
    /// </summary>
    public Task<bool> TryAutoDisableAsync(Guid policyId, int threshold, DateTime nowUtc);
}
