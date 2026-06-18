using Backup.Server.Api.Services;
using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Domain.Options;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
using Backup.Shared.Contracts.DTOs.Jobs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Backup.Server.Tests.Integrity;

public sealed class IntegrityScrubServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<AppDbContext> _options = null!;
    private IDataProtectionProvider _dp = null!;

    public Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _dp = new EphemeralDataProtectionProvider();
        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() { _connection.Dispose(); return Task.CompletedTask; }

    private AppDbContext NewContext() => new(_options, _dp);

    private Guid SeedArtifact(string objectKey, string checksum, long size = 10)
    {
        using var ctx = NewContext();
        var agent = new Agent { Id = Guid.NewGuid(), Name = "a", MachineName = "m", OsType = "linux", Version = "1", Status = AgentStatus.Online };
        var policy = new BackupPolicy { Id = Guid.NewGuid(), AgentId = agent.Id, Name = "p", Type = BackupPolicyType.FileSystem, SourcePath = "/x", IntervalSeconds = 3600 };
        var job = new BackupJob { Id = Guid.NewGuid(), AgentId = agent.Id, PolicyId = policy.Id, Status = BackupJobStatus.Completed };
        var artifact = new BackupArtifact { Id = Guid.NewGuid(), JobId = job.Id, ObjectKey = objectKey, FileName = "f.zip", SizeBytes = size, Checksum = checksum };
        ctx.AddRange(agent, policy, job, artifact);
        ctx.SaveChanges();
        return artifact.Id;
    }

    private IntegrityScrubService BuildService(StubStorage storage, RecordingNotifier notifier)
    {
        var repo = new BackupArtifactRepository(NewContext());
        var audit = new StubAudit();
        return new IntegrityScrubService(
            new SingleScopeFactory(repo, storage, audit, notifier),
            NullLogger<IntegrityScrubService>.Instance,
            Options.Create(new IntegrityOptions()),
            Options.Create(new StorageOptions()));
    }

    [Fact]
    public async Task MatchingChecksum_MarksVerified()
    {
        var id = SeedArtifact("k1", "abc123");
        var storage = new StubStorage(new() { ["k1"] = "ABC123" });
        var notifier = new RecordingNotifier();
        var service = BuildService(storage, notifier);

        var failures = await service.RunScrubAsync(50, CancellationToken.None);

        Assert.Equal(0, failures);
        using var ctx = NewContext();
        var artifact = await ctx.BackupArtifacts.FindAsync(id);
        Assert.Equal(ArtifactIntegrityStatus.Verified, artifact!.IntegrityStatus);
        Assert.NotNull(artifact.LastVerifiedAt);
        Assert.Equal(0, notifier.IntegrityFailures);
    }

    [Fact]
    public async Task Mismatch_MarksFailed_AndNotifies()
    {
        var id = SeedArtifact("k2", "expected");
        var storage = new StubStorage(new() { ["k2"] = "actual-different" });
        var notifier = new RecordingNotifier();
        var service = BuildService(storage, notifier);

        var failures = await service.RunScrubAsync(50, CancellationToken.None);

        Assert.Equal(1, failures);
        using var ctx = NewContext();
        var artifact = await ctx.BackupArtifacts.FindAsync(id);
        Assert.Equal(ArtifactIntegrityStatus.Failed, artifact!.IntegrityStatus);
        Assert.Null(artifact.LastVerifiedAt);
        Assert.Equal(1, notifier.IntegrityFailures);
    }

    [Fact]
    public async Task StorageThrows_MarksFailed()
    {
        var id = SeedArtifact("k3", "whatever");
        var storage = new StubStorage(new()); // key missing -> throws
        var notifier = new RecordingNotifier();
        var service = BuildService(storage, notifier);

        var failures = await service.RunScrubAsync(50, CancellationToken.None);

        Assert.Equal(1, failures);
        using var ctx = NewContext();
        var artifact = await ctx.BackupArtifacts.FindAsync(id);
        Assert.Equal(ArtifactIntegrityStatus.Failed, artifact!.IntegrityStatus);
    }

    private (IntegrityScrubService service, IntegrityScrubSettingsRepository settingsRepo) BuildServiceWithSettings(StubStorage storage, RecordingNotifier notifier)
    {
        var artifactRepo = new BackupArtifactRepository(NewContext());
        var settingsRepo = new IntegrityScrubSettingsRepository(NewContext());
        var service = new IntegrityScrubService(
            new SingleScopeFactory(artifactRepo, storage, new StubAudit(), notifier, settingsRepo),
            NullLogger<IntegrityScrubService>.Instance,
            Options.Create(new IntegrityOptions()),
            Options.Create(new StorageOptions()));
        return (service, settingsRepo);
    }

    [Fact]
    public async Task Tick_Disabled_DoesNotScrub()
    {
        var id = SeedArtifact("d1", "abc");
        var (service, settingsRepo) = BuildServiceWithSettings(new StubStorage(new() { ["d1"] = "abc" }), new RecordingNotifier());

        var settings = await settingsRepo.GetOrCreateAsync(CancellationToken.None);
        settings.IsEnabled = false;
        settings.NextRunAt = DateTime.UtcNow.AddDays(-1); // due, but disabled
        await settingsRepo.UpdateAsync(settings, CancellationToken.None);

        await service.TickAsync(CancellationToken.None);

        using var ctx = NewContext();
        var artifact = await ctx.BackupArtifacts.FindAsync(id);
        Assert.Equal(ArtifactIntegrityStatus.Unverified, artifact!.IntegrityStatus);
    }

    [Fact]
    public async Task Tick_Due_RunsScrub_AndAdvancesNextRun()
    {
        var id = SeedArtifact("d2", "abc");
        var (service, settingsRepo) = BuildServiceWithSettings(new StubStorage(new() { ["d2"] = "ABC" }), new RecordingNotifier());

        var settings = await settingsRepo.GetOrCreateAsync(CancellationToken.None);
        settings.IsEnabled = true;
        settings.NextRunAt = DateTime.UtcNow.AddMinutes(-1);
        await settingsRepo.UpdateAsync(settings, CancellationToken.None);

        await service.TickAsync(CancellationToken.None);

        using var ctx = NewContext();
        var artifact = await ctx.BackupArtifacts.FindAsync(id);
        Assert.Equal(ArtifactIntegrityStatus.Verified, artifact!.IntegrityStatus);

        var after = await settingsRepo.GetOrCreateAsync(CancellationToken.None);
        Assert.True(after.NextRunAt > DateTime.UtcNow);
        Assert.NotNull(after.LastRunAt);
    }

    // --- stubs ---

    private sealed class StubStorage : IStorageAccessService
    {
        private readonly Dictionary<string, string> _hashes;
        public StubStorage(Dictionary<string, string> hashes) { _hashes = hashes; }
        public Task<string> ComputeObjectSha256Async(string objectKey, CancellationToken ct)
            => _hashes.TryGetValue(objectKey, out var h) ? Task.FromResult(h) : throw new InvalidOperationException("missing");
        public Task<StorageObjectInfo> GetObjectInfoAsync(string objectKey, CancellationToken ct) => throw new NotImplementedException();
        public Task EnsureBucketExistsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<UploadTicketResponse> CreateUploadTicketAsync(Guid backupJobId, Guid policyId, Guid agentId, string fileName, string contentType, long sizeBytes, string? publicServerBaseUrl, CancellationToken ct) => throw new NotImplementedException();
        public Task WriteObjectToAsync(string objectKey, Stream destination, CancellationToken ct) => throw new NotImplementedException();
        public Task<string> CreateDownloadTicketAsync(string objectKey, long sizeBytes, string? publicServerBaseUrl, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteObjectAsync(string objectKey, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class StubAudit : IAuditLogRepository
    {
        public Task AddAsync(AuditLog log) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class RecordingNotifier : INotificationService
    {
        public int IntegrityFailures { get; private set; }
        public Task NotifyIntegrityCheckFailedAsync(int failedCount, CancellationToken ct = default) { IntegrityFailures = failedCount; return Task.CompletedTask; }
        public Task NotifyBackupFailedAsync(Guid jobId, Guid policyId, Guid agentId, string errorMessage, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyRestoreFailedAsync(Guid jobId, Guid agentId, string errorMessage, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyBackupCompletedAsync(Guid jobId, Guid policyId, Guid agentId, string policyName, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyAgentOfflineAsync(Guid agentId, string agentName, DateTime? lastSeenAt, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyAgentBackOnlineAsync(Guid agentId, string agentName, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyPolicyAutoDisabledAsync(Guid policyId, Guid agentId, string policyName, int failures, string? lastReason, CancellationToken ct = default) => Task.CompletedTask;
        public Task NotifyRetentionCleanedAsync(int deletedCount, long bytesFreed, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class SingleScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        private readonly IBackupArtifactRepository _repo;
        private readonly IStorageAccessService _storage;
        private readonly IAuditLogRepository _audit;
        private readonly INotificationService _notifier;
        private readonly IIntegrityScrubSettingsRepository? _settings;
        public SingleScopeFactory(IBackupArtifactRepository repo, IStorageAccessService storage, IAuditLogRepository audit, INotificationService notifier, IIntegrityScrubSettingsRepository? settings = null)
        { _repo = repo; _storage = storage; _audit = audit; _notifier = notifier; _settings = settings; }
        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => this;
        public void Dispose() { }
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IBackupArtifactRepository)) return _repo;
            if (serviceType == typeof(IStorageAccessService)) return _storage;
            if (serviceType == typeof(IAuditLogRepository)) return _audit;
            if (serviceType == typeof(INotificationService)) return _notifier;
            if (serviceType == typeof(IIntegrityScrubSettingsRepository)) return _settings;
            return null;
        }
    }
}
