using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Options;
using Backup.Shared.Contracts.DTOs.Agents;
using Backup.Shared.Contracts.DTOs.Jobs;
using Microsoft.Extensions.Options;

namespace Backup.Server.Tests.Policies;

/// <summary>
/// Unit tests for the policy auto-disable streak logic that lives in
/// <see cref="BackupJobsService"/>: a policy is flipped off after
/// <c>AutoDisableThreshold</c> (3) consecutive failed jobs, fires exactly
/// one notification when it trips, and a green run wipes the streak so an
/// isolated later failure doesn't immediately re-trip it.
///
/// Uses hand-rolled in-memory fakes rather than SQLite because the whole
/// behaviour under test is in the service — the repositories just have to
/// hand back the same mutable entity instance across calls.
/// </summary>
public sealed class PolicyAutoDisableTests
{
    private static (BackupJobsService service, FakePolicyRepository policies, FakeNotificationService notifications, FakeAuditLogRepository audit)
        BuildService(BackupPolicy policy, params BackupJob[] jobs)
    {
        var policies = new FakePolicyRepository(policy);
        var jobsRepo = new FakeBackupJobRepository(jobs);
        var artifacts = new FakeBackupArtifactRepository();
        var notifications = new FakeNotificationService();
        var audit = new FakeAuditLogRepository();

        var service = new BackupJobsService(
            policies,
            new FakeAgentRepository(),
            jobsRepo,
            artifacts,
            new FakeStorageAccessService(),
            notifications,
            audit,
            Options.Create(new StorageOptions()));

        return (service, policies, notifications, audit);
    }

    private static BackupPolicy NewPolicy() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = Guid.NewGuid(),
        Type = BackupPolicyType.FileSystem,
        Name = "Nightly /etc",
        SourcePath = "/etc",
        IntervalSeconds = 3600,
        NextRunAt = DateTime.UtcNow,
        IsEnabled = true,
    };

    private static BackupJob RunningJob(BackupPolicy policy) => new()
    {
        Id = Guid.NewGuid(),
        AgentId = policy.AgentId,
        PolicyId = policy.Id,
        Status = BackupJobStatus.Running,
        StartedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task Failed_BelowThreshold_IncrementsStreakButKeepsPolicyEnabled()
    {
        var policy = NewPolicy();
        var job = RunningJob(policy);
        var (service, policies, notifications, audit) = BuildService(policy, job);

        await service.Failed(job.Id, "connection refused");

        Assert.Equal(1, policies.Policy.ConsecutiveFailureCount);
        Assert.True(policies.Policy.IsEnabled);
        Assert.Null(policies.Policy.AutoDisabledAt);
        Assert.Equal("connection refused", policies.Policy.LastFailureReason);
        Assert.Empty(notifications.AutoDisabledCalls);
        Assert.DoesNotContain(audit.Actions, a => a.Action == "policy.auto_disabled");
    }

    [Fact]
    public async Task Failed_AtThreshold_DisablesPolicyAndNotifiesExactlyOnce()
    {
        var policy = NewPolicy();
        var jobA = RunningJob(policy);
        var jobB = RunningJob(policy);
        var jobC = RunningJob(policy);
        var jobD = RunningJob(policy);
        var (service, policies, notifications, audit) = BuildService(policy, jobA, jobB, jobC, jobD);

        await service.Failed(jobA.Id, "boom 1");
        await service.Failed(jobB.Id, "boom 2");
        Assert.True(policies.Policy.IsEnabled); // still enabled after 2

        await service.Failed(jobC.Id, "boom 3"); // trips the threshold

        Assert.False(policies.Policy.IsEnabled);
        Assert.Equal(3, policies.Policy.ConsecutiveFailureCount);
        Assert.NotNull(policies.Policy.AutoDisabledAt);
        Assert.Equal("boom 3", policies.Policy.LastFailureReason);
        Assert.Single(notifications.AutoDisabledCalls);
        Assert.Equal(3, notifications.AutoDisabledCalls[0].Failures);
        Assert.Single(audit.Actions, a => a.Action == "policy.auto_disabled");

        // A further failure on the now-disabled policy must keep counting
        // but must NOT re-fire the auto-disable notification/audit.
        await service.Failed(jobD.Id, "boom 4");

        Assert.Equal(4, policies.Policy.ConsecutiveFailureCount);
        Assert.Single(notifications.AutoDisabledCalls);
        Assert.Single(audit.Actions, a => a.Action == "policy.auto_disabled");
    }

    [Fact]
    public async Task Complete_WipesFailureStreakFromPriorFailures()
    {
        var policy = NewPolicy();
        policy.ConsecutiveFailureCount = 2;
        policy.LastFailureReason = "transient blip";
        var job = RunningJob(policy);
        var (service, policies, _, _) = BuildService(policy, job);

        await service.Complete(job.Id);

        Assert.Equal(0, policies.Policy.ConsecutiveFailureCount);
        Assert.Null(policies.Policy.LastFailureReason);
        Assert.Null(policies.Policy.AutoDisabledAt);
    }

    // ---- fakes -------------------------------------------------------------

    private sealed class FakePolicyRepository(BackupPolicy policy) : IPolicyRepository
    {
        public BackupPolicy Policy { get; } = policy;

        public Task<BackupPolicy?> GetPolicyById(Guid policyId) =>
            Task.FromResult<BackupPolicy?>(policyId == Policy.Id ? Policy : null);

        public Task UpdatePolicy(BackupPolicy policy) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;

        // Mirror the real repository's set-based semantics against the single
        // held entity so the service's orchestration is exercised faithfully.
        public Task IncrementFailureStreakAsync(Guid policyId, string? lastFailureReason)
        {
            if (policyId == Policy.Id)
            {
                Policy.ConsecutiveFailureCount += 1;
                Policy.LastFailureReason = lastFailureReason;
            }

            return Task.CompletedTask;
        }

        public Task<bool> TryAutoDisableAsync(Guid policyId, int threshold, DateTime nowUtc)
        {
            if (policyId == Policy.Id && Policy.IsEnabled && Policy.ConsecutiveFailureCount >= threshold)
            {
                Policy.IsEnabled = false;
                Policy.AutoDisabledAt = nowUtc;
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }

        public Task<List<BackupPolicy>> GetAllPoliciesAsync() => throw new NotImplementedException();
        public Task<BackupPolicy?> GetPolicyByName(Guid agentId, string name) => throw new NotImplementedException();
        public Task<List<BackupPolicy>> GetAllPolicies(Guid agentId) => throw new NotImplementedException();
        public Task AddPolicy(BackupPolicy policy) => throw new NotImplementedException();
        public Task DeletePolicy(BackupPolicy policy) => throw new NotImplementedException();
    }

    private sealed class FakeBackupJobRepository : IBackupJobRepository
    {
        private readonly Dictionary<Guid, BackupJob> _jobs;

        public FakeBackupJobRepository(IEnumerable<BackupJob> jobs) =>
            _jobs = jobs.ToDictionary(j => j.Id);

        public Task<BackupJob?> GetBackupJob(Guid jobId) =>
            Task.FromResult(_jobs.TryGetValue(jobId, out var job) ? job : null);

        public Task UpdateBackupJob(BackupJob job) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task ExecuteInTransactionAsync(Func<Task> action) => action();

        public Task<List<BackupJob>> GetAllBackupJobsAsync() => throw new NotImplementedException();
        public Task<List<BackupJob>> GetBackupJobsByAgentIdAsync(Guid agentId) => throw new NotImplementedException();
        public Task<List<BackupJob>> GetBackupJobsByPolicyIdAsync(Guid policyId) => throw new NotImplementedException();
        public Task AddBackupJob(BackupJob job) => throw new NotImplementedException();
    }

    private sealed class FakeBackupArtifactRepository : IBackupArtifactRepository
    {
        // Complete() refuses to finish a job with zero artifacts, so report one.
        public Task<int> CountByJobIdAsync(Guid jobId) => Task.FromResult(1);

        public Task<List<BackupArtifact>> GetAllArtifactsAsync() => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsByJobIdAsync(Guid jobId) => throw new NotImplementedException();
        public Task<BackupArtifact?> GetArtifactByIdAsync(Guid artifactId) => throw new NotImplementedException();
        public Task AddArtifact(BackupArtifact artifact) => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsForRetentionAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsForScrubAsync(int batchSize, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task UpdateIntegrityAsync(Guid id, Backup.Server.Domain.Enums.ArtifactIntegrityStatus status, DateTime? lastVerifiedAt, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task DeleteArtifactAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task SaveChanges() => throw new NotImplementedException();
    }

    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public List<AuditLog> Actions { get; } = [];

        public Task AddAsync(AuditLog log)
        {
            Actions.Add(log);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed record AutoDisabledCall(Guid PolicyId, int Failures, string? LastReason);

    private sealed class FakeNotificationService : INotificationService
    {
        public List<AutoDisabledCall> AutoDisabledCalls { get; } = [];

        public Task NotifyPolicyAutoDisabledAsync(Guid policyId, Guid agentId, string policyName, int failures, string? lastReason, CancellationToken cancellationToken = default)
        {
            AutoDisabledCalls.Add(new AutoDisabledCall(policyId, failures, lastReason));
            return Task.CompletedTask;
        }

        public Task NotifyBackupFailedAsync(Guid jobId, Guid policyId, Guid agentId, string errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyRestoreFailedAsync(Guid jobId, Guid agentId, string errorMessage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyBackupCompletedAsync(Guid jobId, Guid policyId, Guid agentId, string policyName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyAgentOfflineAsync(Guid agentId, string agentName, DateTime? lastSeenAt, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyAgentBackOnlineAsync(Guid agentId, string agentName, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyRetentionCleanedAsync(int deletedCount, long bytesFreed, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NotifyIntegrityCheckFailedAsync(int failedCount, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStorageAccessService : IStorageAccessService
    {
        public Task EnsureBucketExistsAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<UploadTicketResponse> CreateUploadTicketAsync(Guid backupJobId, Guid policyId, Guid agentId, string fileName, string contentType, long sizeBytes, string? publicServerBaseUrl, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task WriteObjectToAsync(string objectKey, Stream destination, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<StorageObjectInfo> GetObjectInfoAsync(string objectKey, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<string> ComputeObjectSha256Async(string objectKey, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<string> CreateDownloadTicketAsync(string objectKey, long sizeBytes, string? publicServerBaseUrl, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class FakeAgentRepository : IAgentRepository
    {
        public Task<List<Agent>> GetAllAgentsAsync() => throw new NotImplementedException();
        public Task<Agent?> GetByMachineNameAsync(string machineName) => throw new NotImplementedException();
        public Task AddAgent(Agent agent) => throw new NotImplementedException();
        public Task SaveChangesAsync() => throw new NotImplementedException();
        public Task<Agent?> GetAgentByIdAsync(Guid agentId) => throw new NotImplementedException();
        public Task UpdateAgent(Agent agent) => throw new NotImplementedException();
        public Task<int?> GetTokenVersionAsync(Guid agentId) => throw new NotImplementedException();
        public Task<AgentDeletionImpact> GetDeletionImpactAsync(Guid agentId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<List<string>> DeleteAgentAsync(Guid agentId, DeleteAgentOptions options, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
