using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Agents;

namespace Backup.Server.Tests.Policies;

/// <summary>
/// The policy.create audit detail must describe only the schedule half that
/// applies: interval policies carry no dangling "cron=", cron policies no
/// meaningless "interval=0".
/// </summary>
public sealed class PolicyAuditDetailTests
{
    private static readonly Guid AgentId = Guid.NewGuid();

    private static (PoliciesService service, FakeAuditLogRepository audit) BuildService()
    {
        var audit = new FakeAuditLogRepository();
        var service = new PoliciesService(
            new FakePolicyRepository(),
            audit,
            new AdminEventBroadcaster(),
            new FakeAgentRepository(AgentId));
        return (service, audit);
    }

    [Fact]
    public async Task Create_IntervalPolicy_AuditDetailHasIntervalOnly()
    {
        var (service, audit) = BuildService();

        await service.CreatePolicy(
            AgentId, "filesystem", "docs", "/data/docs",
            new PolicyScheduleInput("interval", 3600, null, null, null, null),
            null, null, null, null, Guid.NewGuid());

        var detail = Assert.Single(audit.Details);
        Assert.Contains("schedule=interval interval=3600", detail);
        Assert.DoesNotContain("cron=", detail);
    }

    [Fact]
    public async Task Create_CronPolicy_AuditDetailHasCronOnly()
    {
        var (service, audit) = BuildService();

        await service.CreatePolicy(
            AgentId, "filesystem", "docs", "/data/docs",
            new PolicyScheduleInput("cron", null, "0 3 * * *", "Europe/Moscow", null, null),
            null, null, null, null, Guid.NewGuid());

        var detail = Assert.Single(audit.Details);
        Assert.Contains("schedule=cron cron=0 3 * * * tz=Europe/Moscow", detail);
        Assert.DoesNotContain("interval=", detail);
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public List<string?> Details { get; } = [];

        public Task AddAsync(AuditLog log)
        {
            Details.Add(log.Details);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class FakePolicyRepository : IPolicyRepository
    {
        public Task<BackupPolicy?> GetPolicyByName(Guid agentId, string name) =>
            Task.FromResult<BackupPolicy?>(null);

        public Task AddPolicy(BackupPolicy _) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;

        public Task<BackupPolicy?> GetPolicyById(Guid policyId) => throw new NotImplementedException();
        public Task UpdatePolicy(BackupPolicy _) => throw new NotImplementedException();
        public Task<List<BackupPolicy>> GetAllPoliciesAsync() => throw new NotImplementedException();
        public Task<List<BackupPolicy>> GetAllPolicies(Guid agentId) => throw new NotImplementedException();
        public Task DeletePolicy(BackupPolicy _) => throw new NotImplementedException();
        public Task IncrementFailureStreakAsync(Guid policyId, string? lastFailureReason) => throw new NotImplementedException();
        public Task<bool> TryAutoDisableAsync(Guid policyId, int threshold, DateTime nowUtc) => throw new NotImplementedException();
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
