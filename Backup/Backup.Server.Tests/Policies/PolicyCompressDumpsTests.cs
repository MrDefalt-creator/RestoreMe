using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Agents;

namespace Backup.Server.Tests.Policies;

/// <summary>
/// The per-policy CompressDumps flag must round-trip through create/update and
/// default to true when the request omits it (opt-out, not opt-in).
/// </summary>
public sealed class PolicyCompressDumpsTests
{
    private static readonly Guid AgentId = Guid.NewGuid();

    private static PolicyScheduleInput Interval() =>
        new("interval", 3600, null, null, null, null);

    private static (PoliciesService service, FakePolicyRepository repo) BuildService()
    {
        var repo = new FakePolicyRepository();
        var service = new PoliciesService(
            repo,
            new FakeAuditLogRepository(),
            new AdminEventBroadcaster(),
            new FakeAgentRepository(AgentId));
        return (service, repo);
    }

    [Fact]
    public async Task Create_DefaultsCompressDumpsToTrue()
    {
        var (service, _) = BuildService();

        var policy = await service.CreatePolicy(
            AgentId, "filesystem", "docs", "/data/docs", Interval(),
            null, null, null, null, Guid.NewGuid());

        Assert.True(policy.CompressDumps);
    }

    [Fact]
    public async Task Create_PersistsCompressDumpsFalse()
    {
        var (service, _) = BuildService();

        var policy = await service.CreatePolicy(
            AgentId, "filesystem", "docs", "/data/docs", Interval(),
            null, null, null, null, Guid.NewGuid(), compressDumps: false);

        Assert.False(policy.CompressDumps);
    }

    [Fact]
    public async Task Update_ChangesCompressDumps()
    {
        var (service, repo) = BuildService();

        var created = await service.CreatePolicy(
            AgentId, "filesystem", "docs", "/data/docs", Interval(),
            null, null, null, null, Guid.NewGuid(), compressDumps: true);

        var updated = await service.UpdatePolicy(
            created.Id, AgentId, "filesystem", "docs", "/data/docs", Interval(),
            true, null, null, null, null, Guid.NewGuid(), compressDumps: false);

        Assert.False(updated.CompressDumps);
    }

    private sealed class FakePolicyRepository : IPolicyRepository
    {
        private readonly List<BackupPolicy> _policies = [];

        public Task<BackupPolicy?> GetPolicyByName(Guid agentId, string name) =>
            Task.FromResult(_policies.FirstOrDefault(p => p.AgentId == agentId && p.Name == name));

        public Task<BackupPolicy?> GetPolicyById(Guid policyId) =>
            Task.FromResult(_policies.FirstOrDefault(p => p.Id == policyId));

        public Task AddPolicy(BackupPolicy policy)
        {
            _policies.Add(policy);
            return Task.CompletedTask;
        }

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
}
