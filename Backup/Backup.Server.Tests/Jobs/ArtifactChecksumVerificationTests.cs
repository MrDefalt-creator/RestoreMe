using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Options;
using Backup.Shared.Contracts.DTOs.Agents;
using Backup.Shared.Contracts.DTOs.Jobs;
using Microsoft.Extensions.Options;

namespace Backup.Server.Tests.Jobs;

/// <summary>
/// Verifies the server-side SHA256 integrity gate in
/// <see cref="BackupJobsService.AddArtifact"/>: the stored object is re-hashed
/// and compared to the agent-reported checksum before the artifact is accepted.
/// A mismatch throws (so the job never completes); a match (or a size above the
/// configured re-hash cap) lets the artifact through.
/// </summary>
public sealed class ArtifactChecksumVerificationTests
{
    private static readonly Guid AgentId = Guid.NewGuid();
    private static readonly Guid PolicyId = Guid.NewGuid();
    private static readonly Guid JobId = Guid.NewGuid();
    private const string ObjectKey = "key";

    [Fact]
    public async Task MatchingChecksum_PersistsArtifact()
    {
        var (service, artifacts) = Build(storedSha: "abc123", size: 100, new StorageOptions());

        await service.AddArtifact(JobId, "backup.zip", FullKey(), 100, "ABC123");

        Assert.Single(artifacts.Added);
    }

    [Fact]
    public async Task MismatchedChecksum_Throws_AndDoesNotPersist()
    {
        var (service, artifacts) = Build(storedSha: "deadbeef", size: 100, new StorageOptions());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.AddArtifact(JobId, "backup.zip", FullKey(), 100, "abc123"));

        Assert.Empty(artifacts.Added);
    }

    [Fact]
    public async Task SizeAboveCap_SkipsRehash_AndPersists()
    {
        // Re-hash would throw if invoked; cap forces it to be skipped.
        var options = new StorageOptions { ChecksumVerifyMaxBytes = 50 };
        var (service, artifacts) = Build(storedSha: null, size: 100, options);

        await service.AddArtifact(JobId, "backup.zip", FullKey(), 100, "abc123");

        Assert.Single(artifacts.Added);
    }

    [Fact]
    public async Task VerificationDisabled_SkipsRehash_AndPersists()
    {
        var options = new StorageOptions { VerifyChecksumBeforeComplete = false };
        var (service, artifacts) = Build(storedSha: null, size: 100, options);

        await service.AddArtifact(JobId, "backup.zip", FullKey(), 100, "abc123");

        Assert.Single(artifacts.Added);
    }

    private static string FullKey() => $"{AgentId}/{PolicyId}/{JobId}/backup.zip";

    private static (BackupJobsService service, RecordingArtifactRepo artifacts) Build(
        string? storedSha, long size, StorageOptions options)
    {
        var job = new BackupJob
        {
            Id = JobId,
            Status = BackupJobStatus.Running,
            AgentId = AgentId,
            PolicyId = PolicyId,
        };

        var artifacts = new RecordingArtifactRepo();
        var service = new BackupJobsService(
            new ThrowingPolicyRepo(),
            new ThrowingAgentRepo(),
            new StubJobRepo(job),
            artifacts,
            new StubStorage(storedSha, size),
            new ThrowingNotificationService(),
            new CollectingAuditRepo(),
            Options.Create(options));

        return (service, artifacts);
    }

    private sealed class StubStorage : IStorageAccessService
    {
        private readonly string? _sha;
        private readonly long _size;
        public StubStorage(string? sha, long size) { _sha = sha; _size = size; }

        public Task<StorageObjectInfo> GetObjectInfoAsync(string objectKey, CancellationToken ct)
            => Task.FromResult(new StorageObjectInfo(_size));

        public Task<string> ComputeObjectSha256Async(string objectKey, CancellationToken ct)
            => _sha is null
                ? throw new InvalidOperationException("re-hash should have been skipped")
                : Task.FromResult(_sha);

        public Task EnsureBucketExistsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<UploadTicketResponse> CreateUploadTicketAsync(Guid backupJobId, Guid policyId, Guid agentId, string fileName, string contentType, long sizeBytes, string? publicServerBaseUrl, CancellationToken ct) => throw new NotImplementedException();
        public Task WriteObjectToAsync(string objectKey, Stream destination, CancellationToken ct) => throw new NotImplementedException();
        public Task<string> CreateDownloadTicketAsync(string objectKey, long sizeBytes, string? publicServerBaseUrl, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteObjectAsync(string objectKey, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class RecordingArtifactRepo : IBackupArtifactRepository
    {
        public List<BackupArtifact> Added { get; } = [];
        public Task AddArtifact(BackupArtifact artifact) { Added.Add(artifact); return Task.CompletedTask; }
        public Task SaveChanges() => Task.CompletedTask;

        public Task<List<BackupArtifact>> GetAllArtifactsAsync() => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsByJobIdAsync(Guid jobId) => throw new NotImplementedException();
        public Task<BackupArtifact?> GetArtifactByIdAsync(Guid artifactId) => throw new NotImplementedException();
        public Task<int> CountByJobIdAsync(Guid jobId) => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsForRetentionAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteArtifactAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class StubJobRepo : IBackupJobRepository
    {
        private readonly BackupJob _job;
        public StubJobRepo(BackupJob job) { _job = job; }
        public Task<BackupJob?> GetBackupJob(Guid jobId) => Task.FromResult<BackupJob?>(_job);

        public Task<List<BackupJob>> GetAllBackupJobsAsync() => throw new NotImplementedException();
        public Task<List<BackupJob>> GetBackupJobsByAgentIdAsync(Guid agentId) => throw new NotImplementedException();
        public Task<List<BackupJob>> GetBackupJobsByPolicyIdAsync(Guid policyId) => throw new NotImplementedException();
        public Task AddBackupJob(BackupJob job) => throw new NotImplementedException();
        public Task SaveChangesAsync() => throw new NotImplementedException();
        public Task UpdateBackupJob(BackupJob job) => throw new NotImplementedException();
        public Task ExecuteInTransactionAsync(Func<Task> action) => throw new NotImplementedException();
    }

    private sealed class CollectingAuditRepo : IAuditLogRepository
    {
        public Task AddAsync(AuditLog log) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class ThrowingPolicyRepo : IPolicyRepository
    {
        public Task<List<BackupPolicy>> GetAllPoliciesAsync() => throw new NotImplementedException();
        public Task<BackupPolicy?> GetPolicyByName(Guid agentId, string name) => throw new NotImplementedException();
        public Task<List<BackupPolicy>> GetAllPolicies(Guid agentId) => throw new NotImplementedException();
        public Task<BackupPolicy?> GetPolicyById(Guid policyId) => throw new NotImplementedException();
        public Task AddPolicy(BackupPolicy policy) => throw new NotImplementedException();
        public Task UpdatePolicy(BackupPolicy policy) => throw new NotImplementedException();
        public Task DeletePolicy(BackupPolicy policy) => throw new NotImplementedException();
        public Task SaveChangesAsync() => throw new NotImplementedException();
        public Task IncrementFailureStreakAsync(Guid policyId, string? lastFailureReason) => throw new NotImplementedException();
        public Task<bool> TryAutoDisableAsync(Guid policyId, int threshold, DateTime nowUtc) => throw new NotImplementedException();
    }

    private sealed class ThrowingAgentRepo : IAgentRepository
    {
        public Task<List<Agent>> GetAllAgentsAsync() => throw new NotImplementedException();
        public Task<Agent?> GetByMachineNameAsync(string machineName) => throw new NotImplementedException();
        public Task AddAgent(Agent agent) => throw new NotImplementedException();
        public Task SaveChangesAsync() => throw new NotImplementedException();
        public Task<Agent?> GetAgentByIdAsync(Guid agentId) => throw new NotImplementedException();
        public Task UpdateAgent(Agent agent) => throw new NotImplementedException();
        public Task<int?> GetTokenVersionAsync(Guid agentId) => throw new NotImplementedException();
        public Task<AgentDeletionImpact> GetDeletionImpactAsync(Guid agentId, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<string>> DeleteAgentAsync(Guid agentId, DeleteAgentOptions options, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class ThrowingNotificationService : INotificationService
    {
        public Task NotifyBackupFailedAsync(Guid jobId, Guid policyId, Guid agentId, string errorMessage, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyRestoreFailedAsync(Guid jobId, Guid agentId, string errorMessage, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyBackupCompletedAsync(Guid jobId, Guid policyId, Guid agentId, string policyName, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyAgentOfflineAsync(Guid agentId, string agentName, DateTime? lastSeenAt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyAgentBackOnlineAsync(Guid agentId, string agentName, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyPolicyAutoDisabledAsync(Guid policyId, Guid agentId, string policyName, int failures, string? lastReason, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyRetentionCleanedAsync(int deletedCount, long bytesFreed, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyIntegrityCheckFailedAsync(int failedCount, CancellationToken ct = default) => Task.CompletedTask;
    }
}
