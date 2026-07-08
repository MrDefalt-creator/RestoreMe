using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs.Agents;

namespace Backup.Server.Tests.Policies;

/// <summary>
/// Create/update must reject an agent id that doesn't resolve to a real
/// agent (including Guid.Empty from a request body that omitted agentId)
/// with a clean 404-mapped KeyNotFoundException instead of surfacing a
/// 500 FK violation from the database.
/// </summary>
public sealed class PolicyAgentValidationTests
{
    private static readonly Guid KnownAgentId = Guid.NewGuid();

    private static PolicyScheduleInput IntervalSchedule() =>
        new("interval", 3600, null, null, null, null);

    private static (PoliciesService service, BackupPolicy policy) BuildService()
    {
        var policy = new BackupPolicy
        {
            Id = Guid.NewGuid(),
            AgentId = KnownAgentId,
            Type = BackupPolicyType.FileSystem,
            Name = "Nightly /etc",
            SourcePath = "/etc",
            IntervalSeconds = 3600,
            NextRunAt = DateTime.UtcNow,
            IsEnabled = true,
        };

        var service = new PoliciesService(
            new FakePolicyRepository(policy),
            new FakeAuditLogRepository(),
            new AdminEventBroadcaster(),
            new FakeAgentRepository(KnownAgentId));

        return (service, policy);
    }

    [Fact]
    public async Task CreatePolicy_UnknownAgent_ThrowsNotFound()
    {
        var (service, _) = BuildService();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => service.CreatePolicy(
            Guid.NewGuid(), "filesystem", "docs", "/data/docs",
            IntervalSchedule(), null, null, null, null, Guid.NewGuid()));

        Assert.Equal("Agent not found.", ex.Message);
    }

    [Fact]
    public async Task UpdatePolicy_EmptyAgentId_ThrowsNotFound()
    {
        var (service, policy) = BuildService();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.UpdatePolicy(
            policy.Id, Guid.Empty, "filesystem", policy.Name, policy.SourcePath,
            IntervalSchedule(), true, null, null, null, null, Guid.NewGuid()));

        Assert.Equal(KnownAgentId, policy.AgentId);
    }

    [Fact]
    public async Task UpdatePolicy_KnownAgent_Succeeds()
    {
        var (service, policy) = BuildService();

        var updated = await service.UpdatePolicy(
            policy.Id, KnownAgentId, "filesystem", "renamed", policy.SourcePath,
            IntervalSchedule(), true, null, null, null, null, Guid.NewGuid());

        Assert.Equal("renamed", updated.Name);
        Assert.Equal(KnownAgentId, updated.AgentId);
    }

    private sealed class FakeAgentRepository(Guid knownAgentId) : IAgentRepository
    {
        public Task<Agent?> GetAgentByIdAsync(Guid agentId) =>
            Task.FromResult<Agent?>(agentId == knownAgentId
                ? new Agent { Id = knownAgentId, MachineName = "host", Name = "host" }
                : null);

        public Task<List<Agent>> GetAllAgentsAsync() => throw new NotImplementedException();
        public Task<PagedResult<Agent>> QueryAgentsAsync(PagedQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<Agent?> GetByMachineNameAsync(string machineName) => throw new NotImplementedException();
        public Task AddAgent(Agent agent) => throw new NotImplementedException();
        public Task SaveChangesAsync() => throw new NotImplementedException();
        public Task UpdateAgent(Agent agent) => throw new NotImplementedException();
        public Task<int?> GetTokenVersionAsync(Guid agentId) => throw new NotImplementedException();
        public Task<AgentDeletionImpact> GetDeletionImpactAsync(Guid agentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<List<string>> DeleteAgentAsync(Guid agentId, DeleteAgentOptions options, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class FakePolicyRepository(BackupPolicy policy) : IPolicyRepository
    {
        public Task<BackupPolicy?> GetPolicyById(Guid policyId) =>
            Task.FromResult<BackupPolicy?>(policyId == policy.Id ? policy : null);

        public Task<BackupPolicy?> GetPolicyByName(Guid agentId, string name) =>
            Task.FromResult<BackupPolicy?>(null);

        public Task AddPolicy(BackupPolicy _) => Task.CompletedTask;
        public Task UpdatePolicy(BackupPolicy _) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;

        public Task<List<BackupPolicy>> GetAllPoliciesAsync() => throw new NotImplementedException();
        public Task<List<BackupPolicy>> GetAllPolicies(Guid agentId) => throw new NotImplementedException();
        public Task DeletePolicy(BackupPolicy _) => throw new NotImplementedException();
        public Task IncrementFailureStreakAsync(Guid policyId, string? lastFailureReason) => throw new NotImplementedException();
        public Task<bool> TryAutoDisableAsync(Guid policyId, int threshold, DateTime nowUtc) => throw new NotImplementedException();
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public Task AddAsync(AuditLog log) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
