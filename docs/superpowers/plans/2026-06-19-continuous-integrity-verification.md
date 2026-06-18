# Continuous Integrity Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Detect at-rest artifact corruption proactively (scheduled scrub) and refuse to apply a corrupt restore, with the integrity state surfaced in the admin UI.

**Architecture:** A new `IntegrityScrubService` background worker (modeled on `RetentionCleanupService`) re-hashes stored artifacts in throttled batches and records per-artifact integrity state on `BackupArtifact`. The agent's `RestoreExecuter` verifies the downloaded artifact's SHA256 against the expected checksum before touching the restore target. The artifacts page shows an integrity badge + a manual "Verify now" action.

**Tech Stack:** ASP.NET Core 10, EF Core (PostgreSQL prod / SQLite tests), xUnit, React + TanStack Query (Frontend-2.0).

## Global Constraints

- Clean-architecture layering: `Domain` has no deps; `Application` holds interfaces + services; `Infrastructure` holds EF repos; `Api` is the DI root. Copy existing patterns.
- TDD: write the failing test first, watch it fail, implement minimally, watch it pass, commit.
- Reuse existing primitives: `IStorageAccessService.ComputeObjectSha256Async`, `BackupArtifact.Checksum`, agent `IChecksumService.ComputeSha256Async`, `StorageOptions.ChecksumVerifyMaxBytes`.
- Background-service + options pattern: follow `RetentionCleanupService` / `RetentionOptions` exactly.
- Audit system actions use `ActorId = null`. Notifications are best-effort (never bubble).
- Run backend tests with: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx`
- Build with: `dotnet build D:/projects/RestorMe/Backup/BackupSystem.slnx --configuration Debug`
- Frontend checks: `cd Frontend-2.0 && yarn typecheck && yarn lint`
- Branch: `feature/retention-and-integrity`. `main` is frozen — do not touch. Do not stage the root-level `.docx`/`.pptx` files.
- Migrations auto-apply on startup; generate with the EF command from CLAUDE.md.

---

### Task 1: Domain — integrity state on BackupArtifact + migration

**Files:**
- Create: `Backup/Backup.Server.Domain/Enums/ArtifactIntegrityStatus.cs`
- Modify: `Backup/Backup.Server.Domain/Entities/BackupArtifact.cs`
- Create (generated): `Backup/Backup.Server.Infrastructure/Migrations/<timestamp>_AddArtifactIntegrityState.cs` (+ `.Designer.cs`)
- Modify (generated): `Backup/Backup.Server.Infrastructure/Migrations/AppDbContextModelSnapshot.cs`

**Interfaces:**
- Produces: `enum ArtifactIntegrityStatus { Unverified = 0, Verified = 1, Failed = 2 }`; `BackupArtifact.IntegrityStatus` (`ArtifactIntegrityStatus`), `BackupArtifact.LastVerifiedAt` (`DateTime?`).

- [ ] **Step 1: Create the enum**

```csharp
// Backup/Backup.Server.Domain/Enums/ArtifactIntegrityStatus.cs
namespace Backup.Server.Domain.Enums;

public enum ArtifactIntegrityStatus
{
    Unverified = 0,
    Verified = 1,
    Failed = 2,
}
```

- [ ] **Step 2: Add fields to BackupArtifact**

Add `using Backup.Server.Domain.Enums;` at the top, then add these properties after `Checksum`:

```csharp
    // Result of the most recent integrity check (scrub or manual verify).
    public ArtifactIntegrityStatus IntegrityStatus { get; set; } = ArtifactIntegrityStatus.Unverified;

    // When the stored object was last successfully re-hashed and matched.
    public DateTime? LastVerifiedAt { get; set; }
```

- [ ] **Step 3: Generate the migration**

Run:
```bash
cd D:/projects/RestorMe/Backup
dotnet ef migrations add AddArtifactIntegrityState --project ./Backup.Server.Infrastructure/Backup.Server.Infrastructure.csproj --startup-project ./Backup.Server.Api/Backup.Server.Api.csproj --output-dir Migrations
```
Expected: new migration files created; `IntegrityStatus` (int, default 0) and `LastVerifiedAt` (nullable timestamp) added to `BackupArtifacts`.

- [ ] **Step 4: Build to verify the migration compiles**

Run: `dotnet build D:/projects/RestorMe/Backup/BackupSystem.slnx --configuration Debug`
Expected: `Сборка успешно завершена` / Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
cd D:/projects/RestorMe
git add Backup/Backup.Server.Domain Backup/Backup.Server.Infrastructure/Migrations
git commit -m "feat(integrity): add integrity state to BackupArtifact + migration"
```

---

### Task 2: Notification event — IntegrityCheckFailed

**Files:**
- Modify: `Backup/Backup.Server.Domain/Enums/NotificationEventType.cs`
- Modify: `Backup/Backup.Server.Application/Interfaces/INotificationService.cs`
- Modify: `Backup/Backup.Server.Application/Services/NotificationDispatcher.cs`
- Modify: `Backup/Backup.Server.Tests/Jobs/ArtifactChecksumVerificationTests.cs` (the `ThrowingNotificationService` stub must implement the new method)

**Interfaces:**
- Produces: `NotificationEventType.IntegrityCheckFailed = 7`; `INotificationService.NotifyIntegrityCheckFailedAsync(int failedCount, CancellationToken)`.

- [ ] **Step 1: Add the enum value**

In `NotificationEventType.cs`, after `RetentionCleaned = 6,`:
```csharp
    IntegrityCheckFailed = 7,
```

- [ ] **Step 2: Add the interface method**

In `INotificationService.cs`, after `NotifyRetentionCleanedAsync`:
```csharp
    Task NotifyIntegrityCheckFailedAsync(
        int failedCount,
        CancellationToken cancellationToken = default);
```

- [ ] **Step 3: Implement in the dispatcher**

In `NotificationDispatcher.cs`, after `NotifyRetentionCleanedAsync`:
```csharp
    public Task NotifyIntegrityCheckFailedAsync(
        int failedCount,
        CancellationToken cancellationToken = default)
    {
        var evt = new NotificationEvent(
            NotificationEventType.IntegrityCheckFailed,
            "Artifact integrity check failed",
            $"{failedCount} stored artifact(s) failed SHA256 re-verification",
            "Run a manual verify or inspect the audit log for the affected artifacts.",
            DateTime.UtcNow,
            new Dictionary<string, string?>
            {
                ["failedCount"] = failedCount.ToString(CultureInfo.InvariantCulture),
            });

        return DispatchAsync(evt, cancellationToken);
    }
```

- [ ] **Step 4: Update the test stub so the suite still compiles**

In `ArtifactChecksumVerificationTests.cs`, inside `ThrowingNotificationService`, after the `NotifyRetentionCleanedAsync` line:
```csharp
        public Task NotifyIntegrityCheckFailedAsync(int failedCount, CancellationToken ct = default) => Task.CompletedTask;
```

- [ ] **Step 5: Build + run tests**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx`
Expected: build succeeds; all existing tests still pass (53+).

- [ ] **Step 6: Commit**

```bash
cd D:/projects/RestorMe
git add Backup/Backup.Server.Domain Backup/Backup.Server.Application Backup/Backup.Server.Tests
git commit -m "feat(integrity): IntegrityCheckFailed notification event"
```

---

### Task 3: Scrub decision helper + options + repository methods

**Files:**
- Create: `Backup/Backup.Server.Application/Services/IntegrityScrubDecision.cs`
- Create: `Backup/Backup.Server.Domain/Options/IntegrityOptions.cs`
- Modify: `Backup/Backup.Server.Application/Interfaces/IBackupArtifactRepository.cs`
- Modify: `Backup/Backup.Server.Infrastructure/Services/BackupArtifactRepository.cs`
- Modify: `Backup/Backup.Server.Tests/Jobs/ArtifactChecksumVerificationTests.cs` (the `RecordingArtifactRepo` stub must implement the new methods)
- Create test: `Backup/Backup.Server.Tests/Integrity/IntegrityScrubDecisionTests.cs`

**Interfaces:**
- Produces:
  - `enum ScrubOutcome { Skipped, Verified, Failed }`
  - `static ScrubOutcome IntegrityScrubDecision.Evaluate(long sizeBytes, long? maxBytes, string expectedChecksum, string? computedChecksum)`
  - `IntegrityOptions { const string SectionName = "Integrity"; int ScrubIntervalHours = 168; int ScrubBatchSize = 50; }`
  - `IBackupArtifactRepository.GetArtifactsForScrubAsync(int batchSize, CancellationToken) : Task<List<BackupArtifact>>`
  - `IBackupArtifactRepository.UpdateIntegrityAsync(Guid id, ArtifactIntegrityStatus status, DateTime? lastVerifiedAt, CancellationToken) : Task`

- [ ] **Step 1: Write the failing decision test**

```csharp
// Backup/Backup.Server.Tests/Integrity/IntegrityScrubDecisionTests.cs
using Backup.Server.Application.Services;

namespace Backup.Server.Tests.Integrity;

public sealed class IntegrityScrubDecisionTests
{
    [Fact]
    public void OverSizeCap_Skips()
    {
        var result = IntegrityScrubDecision.Evaluate(sizeBytes: 100, maxBytes: 50, expectedChecksum: "abc", computedChecksum: "abc");
        Assert.Equal(ScrubOutcome.Skipped, result);
    }

    [Fact]
    public void WithinCap_MatchingChecksum_Verifies()
    {
        var result = IntegrityScrubDecision.Evaluate(100, maxBytes: 200, expectedChecksum: "ABC", computedChecksum: "abc");
        Assert.Equal(ScrubOutcome.Verified, result);
    }

    [Fact]
    public void WithinCap_Mismatch_Fails()
    {
        var result = IntegrityScrubDecision.Evaluate(100, maxBytes: null, expectedChecksum: "abc", computedChecksum: "deadbeef");
        Assert.Equal(ScrubOutcome.Failed, result);
    }

    [Fact]
    public void NullComputed_Fails()
    {
        var result = IntegrityScrubDecision.Evaluate(100, maxBytes: null, expectedChecksum: "abc", computedChecksum: null);
        Assert.Equal(ScrubOutcome.Failed, result);
    }
}
```

- [ ] **Step 2: Run it, expect FAIL (type not defined)**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx --filter "FullyQualifiedName~IntegrityScrubDecisionTests"`
Expected: compile failure / FAIL — `IntegrityScrubDecision` does not exist.

- [ ] **Step 3: Implement the decision helper**

```csharp
// Backup/Backup.Server.Application/Services/IntegrityScrubDecision.cs
namespace Backup.Server.Application.Services;

public enum ScrubOutcome
{
    Skipped,
    Verified,
    Failed,
}

/// <summary>
/// Pure decision for a single artifact scrub: over the size cap -> Skipped;
/// computed SHA256 equals the expected (case-insensitive) -> Verified; else Failed.
/// </summary>
public static class IntegrityScrubDecision
{
    public static ScrubOutcome Evaluate(
        long sizeBytes,
        long? maxBytes,
        string expectedChecksum,
        string? computedChecksum)
    {
        if (maxBytes is not null && sizeBytes > maxBytes.Value)
        {
            return ScrubOutcome.Skipped;
        }

        if (!string.IsNullOrWhiteSpace(computedChecksum)
            && string.Equals(computedChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase))
        {
            return ScrubOutcome.Verified;
        }

        return ScrubOutcome.Failed;
    }
}
```

- [ ] **Step 4: Run the decision test, expect PASS**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx --filter "FullyQualifiedName~IntegrityScrubDecisionTests"`
Expected: PASS (4 tests).

- [ ] **Step 5: Create the options class**

```csharp
// Backup/Backup.Server.Domain/Options/IntegrityOptions.cs
namespace Backup.Server.Domain.Options;

public class IntegrityOptions
{
    public const string SectionName = "Integrity";

    // How often the scrub sweep runs. Defaults to weekly.
    public int ScrubIntervalHours { get; set; } = 168;

    // Max artifacts re-hashed per tick (throttles MinIO I/O).
    public int ScrubBatchSize { get; set; } = 50;
}
```

- [ ] **Step 6: Add the repository interface methods**

In `IBackupArtifactRepository.cs` add `using Backup.Server.Domain.Enums;` and, after `GetArtifactsForRetentionAsync`:
```csharp
    // The next batch of artifacts to scrub, least-recently-verified first
    // (NULL LastVerifiedAt before any timestamp). Capped at batchSize.
    public Task<List<BackupArtifact>> GetArtifactsForScrubAsync(int batchSize, CancellationToken cancellationToken);

    // Persists the integrity outcome for a single artifact without loading it.
    public Task UpdateIntegrityAsync(Guid id, ArtifactIntegrityStatus status, DateTime? lastVerifiedAt, CancellationToken cancellationToken);
```

- [ ] **Step 7: Implement the repository methods**

In `BackupArtifactRepository.cs` add `using Backup.Server.Domain.Enums;` and, after `GetArtifactsForRetentionAsync`:
```csharp
    public async Task<List<BackupArtifact>> GetArtifactsForScrubAsync(int batchSize, CancellationToken cancellationToken)
    {
        return await _dbContext.BackupArtifacts
            .AsNoTracking()
            .OrderBy(a => a.LastVerifiedAt == null ? 0 : 1)
            .ThenBy(a => a.LastVerifiedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateIntegrityAsync(Guid id, ArtifactIntegrityStatus status, DateTime? lastVerifiedAt, CancellationToken cancellationToken)
    {
        await _dbContext.BackupArtifacts
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.IntegrityStatus, status)
                .SetProperty(a => a.LastVerifiedAt, lastVerifiedAt),
                cancellationToken);
    }
```

- [ ] **Step 8: Update the RecordingArtifactRepo test stub**

In `ArtifactChecksumVerificationTests.cs`, inside `RecordingArtifactRepo`, after the `GetArtifactsForRetentionAsync` line add:
```csharp
        public Task<List<BackupArtifact>> GetArtifactsForScrubAsync(int batchSize, CancellationToken ct) => throw new NotImplementedException();
        public Task UpdateIntegrityAsync(Guid id, Backup.Server.Domain.Enums.ArtifactIntegrityStatus status, DateTime? lastVerifiedAt, CancellationToken ct) => throw new NotImplementedException();
```

- [ ] **Step 9: Build + run full suite**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx`
Expected: build succeeds; all tests pass.

- [ ] **Step 10: Commit**

```bash
cd D:/projects/RestorMe
git add Backup/Backup.Server.Application Backup/Backup.Server.Domain Backup/Backup.Server.Infrastructure Backup/Backup.Server.Tests
git commit -m "feat(integrity): scrub decision helper, options, repo scrub/update methods"
```

---

### Task 4: IntegrityScrubService background worker

**Files:**
- Create: `Backup/Backup.Server.Api/Services/IntegrityScrubService.cs`
- Modify: `Backup/Backup.Server.Api/Program.cs` (bind options + register hosted service)
- Modify: `Backup/Backup.Server.Api/appsettings.example.json`
- Create test: `Backup/Backup.Server.Tests/Integrity/IntegrityScrubServiceTests.cs`

**Interfaces:**
- Consumes: `IBackupArtifactRepository.GetArtifactsForScrubAsync` / `UpdateIntegrityAsync`, `IStorageAccessService.ComputeObjectSha256Async`, `IAuditLogRepository`, `INotificationService.NotifyIntegrityCheckFailedAsync`, `IntegrityScrubDecision.Evaluate`, `IntegrityOptions`, `StorageOptions.ChecksumVerifyMaxBytes`.
- Produces: `IntegrityScrubService.RunScrubAsync(CancellationToken)` (internal, test-callable) returning `Task<int>` (failure count).

- [ ] **Step 1: Write the failing scrub-service test**

This integration test seeds artifacts into SQLite and drives one scrub pass through real repo + stub storage.

```csharp
// Backup/Backup.Server.Tests/Integrity/IntegrityScrubServiceTests.cs
using Backup.Server.Api.Services;
using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Domain.Options;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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
        var policy = new BackupPolicy { Id = Guid.NewGuid(), AgentId = agent.Id, Name = "p", Type = BackupPolicyType.FileSystem, SourcePath = "/x", Schedule = "* * * * *" };
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
            Options.Create(new IntegrityOptions { ScrubBatchSize = 50 }),
            Options.Create(new StorageOptions()));
    }

    [Fact]
    public async Task MatchingChecksum_MarksVerified()
    {
        var id = SeedArtifact("k1", "abc123");
        var storage = new StubStorage(new() { ["k1"] = "ABC123" });
        var notifier = new RecordingNotifier();
        var service = BuildService(storage, notifier);

        var failures = await service.RunScrubAsync(CancellationToken.None);

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

        var failures = await service.RunScrubAsync(CancellationToken.None);

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

        var failures = await service.RunScrubAsync(CancellationToken.None);

        Assert.Equal(1, failures);
        using var ctx = NewContext();
        var artifact = await ctx.BackupArtifacts.FindAsync(id);
        Assert.Equal(ArtifactIntegrityStatus.Failed, artifact!.IntegrityStatus);
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

    // Resolves the four scoped services the worker asks for from a single set of instances.
    private sealed class SingleScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
    {
        private readonly IBackupArtifactRepository _repo;
        private readonly IStorageAccessService _storage;
        private readonly IAuditLogRepository _audit;
        private readonly INotificationService _notifier;
        public SingleScopeFactory(IBackupArtifactRepository repo, IStorageAccessService storage, IAuditLogRepository audit, INotificationService notifier)
        { _repo = repo; _storage = storage; _audit = audit; _notifier = notifier; }
        public IServiceScope CreateScope() => this;
        public IServiceProvider ServiceProvider => this;
        public void Dispose() { }
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IBackupArtifactRepository)) return _repo;
            if (serviceType == typeof(IStorageAccessService)) return _storage;
            if (serviceType == typeof(IAuditLogRepository)) return _audit;
            if (serviceType == typeof(INotificationService)) return _notifier;
            return null;
        }
    }
}
```

> Note: confirm the `BackupPolicy` / `BackupJob` seed property names compile (e.g. `Schedule`, `BackupPolicyType.FileSystem`, `BackupJobStatus.Completed`). If a name differs, fix the seed to match the entity — the assertions are what matter.

- [ ] **Step 2: Run it, expect FAIL (service not defined)**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx --filter "FullyQualifiedName~IntegrityScrubServiceTests"`
Expected: compile failure — `IntegrityScrubService` does not exist.

- [ ] **Step 3: Implement the service**

```csharp
// Backup/Backup.Server.Api/Services/IntegrityScrubService.cs
using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Domain.Options;
using Backup.Server.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Backup.Server.Api.Services;

public class IntegrityScrubService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IntegrityScrubService> _logger;
    private readonly IntegrityOptions _integrityOptions;
    private readonly StorageOptions _storageOptions;
    private readonly TimeSpan _interval;

    public IntegrityScrubService(
        IServiceScopeFactory scopeFactory,
        ILogger<IntegrityScrubService> logger,
        IOptions<IntegrityOptions> integrityOptions,
        IOptions<StorageOptions> storageOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _integrityOptions = integrityOptions.Value;
        _storageOptions = storageOptions.Value;
        var hours = _integrityOptions.ScrubIntervalHours;
        _interval = TimeSpan.FromHours(hours > 0 ? hours : 168);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunScrubAsync(stoppingToken);
            }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Integrity scrub sweep failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    internal async Task<int> RunScrubAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var artifactRepo = scope.ServiceProvider.GetRequiredService<IBackupArtifactRepository>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageAccessService>();
        var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var batch = await artifactRepo.GetArtifactsForScrubAsync(_integrityOptions.ScrubBatchSize, cancellationToken);
        if (batch.Count == 0)
        {
            return 0;
        }

        var failures = 0;
        foreach (var artifact in batch)
        {
            string? computed = null;
            string? error = null;
            try
            {
                if (_storageOptions.ChecksumVerifyMaxBytes is null
                    || artifact.SizeBytes <= _storageOptions.ChecksumVerifyMaxBytes.Value)
                {
                    computed = await storage.ComputeObjectSha256Async(artifact.ObjectKey, cancellationToken);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            var outcome = error is not null
                ? ScrubOutcome.Failed
                : IntegrityScrubDecision.Evaluate(
                    artifact.SizeBytes,
                    _storageOptions.ChecksumVerifyMaxBytes,
                    artifact.Checksum,
                    computed);

            switch (outcome)
            {
                case ScrubOutcome.Skipped:
                    await auditRepo.AddAsync(SystemAudit("artifact.scrub_skipped", artifact.Id,
                        $"objectKey={artifact.ObjectKey} size={artifact.SizeBytes} limit={_storageOptions.ChecksumVerifyMaxBytes}"));
                    break;

                case ScrubOutcome.Verified:
                    await artifactRepo.UpdateIntegrityAsync(artifact.Id, ArtifactIntegrityStatus.Verified, DateTime.UtcNow, cancellationToken);
                    break;

                case ScrubOutcome.Failed:
                    failures++;
                    await artifactRepo.UpdateIntegrityAsync(artifact.Id, ArtifactIntegrityStatus.Failed, null, cancellationToken);
                    await auditRepo.AddAsync(SystemAudit("artifact.scrub_failed", artifact.Id,
                        error is not null
                            ? $"objectKey={artifact.ObjectKey} error={error}"
                            : $"objectKey={artifact.ObjectKey} expected={artifact.Checksum} actual={computed}"));
                    break;
            }
        }

        await auditRepo.SaveChangesAsync();

        if (failures > 0)
        {
            await notifications.NotifyIntegrityCheckFailedAsync(failures, cancellationToken);
        }

        _logger.LogInformation("Integrity scrub: checked {Count} artifact(s), {Failures} failure(s)", batch.Count, failures);
        return failures;
    }

    private static AuditLog SystemAudit(string action, Guid targetId, string details) => new()
    {
        Id = Guid.NewGuid(),
        ActorId = null,
        Action = action,
        TargetId = targetId,
        Details = details,
        OccurredAt = DateTime.UtcNow,
    };
}
```

- [ ] **Step 4: Register options + hosted service in Program.cs**

After the `RetentionOptions` binding block (the `AddOptions<RetentionOptions>()...Bind(...)` lines), add:
```csharp
builder.Services
    .AddOptions<IntegrityOptions>()
    .Bind(builder.Configuration.GetSection(IntegrityOptions.SectionName));
```
And after `builder.Services.AddHostedService<RetentionCleanupService>();`:
```csharp
builder.Services.AddHostedService<IntegrityScrubService>();
```

- [ ] **Step 5: Run the scrub tests, expect PASS**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx --filter "FullyQualifiedName~IntegrityScrubServiceTests"`
Expected: PASS (3 tests). If a seed property name failed to compile, fix the seed (not the assertions) and re-run.

- [ ] **Step 6: Document config in appsettings.example.json**

After the `"Retention": { ... }` block, add:
```jsonc
  "Integrity": {
    "_": "Background scrub re-hashes stored artifacts to detect at-rest bit-rot. ScrubBatchSize throttles MinIO I/O per tick; objects larger than Storage:ChecksumVerifyMaxBytes are skipped (existence/size still effectively checked on access).",
    "ScrubIntervalHours": 168,
    "ScrubBatchSize": 50
  },
```

- [ ] **Step 7: Full suite + build**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx`
Expected: all pass.

- [ ] **Step 8: Commit**

```bash
cd D:/projects/RestorMe
git add Backup/Backup.Server.Api Backup/Backup.Server.Tests
git commit -m "feat(integrity): scheduled scrub sweep background service"
```

---

### Task 5: Manual "Verify now" endpoint

**Files:**
- Modify: `Backup/Backup.Server.Application/Services/BackupArtifactsService.cs`
- Modify: `Backup/Backup.Server.Api/Controllers/BackupArtifactsController.cs`
- Create: `Backup/Backup.Shared.Contracts/DTOs/Artifacts/ArtifactVerifyResultDto.cs`
- Create test: `Backup/Backup.Server.Tests/Integrity/ManualVerifyTests.cs`

**Interfaces:**
- Consumes: `IStorageAccessService.ComputeObjectSha256Async`, `IBackupArtifactRepository.GetArtifactByIdAsync` / `UpdateIntegrityAsync`, `IAuditLogRepository`, `StorageOptions`, `IntegrityScrubDecision`.
- Produces:
  - `record ArtifactVerifyResultDto(Guid Id, string IntegrityStatus, DateTime? LastVerifiedAt)`
  - `BackupArtifactsService.VerifyArtifactAsync(Guid artifactId, Guid? actorId, CancellationToken) : Task<ArtifactVerifyResultDto>`

- [ ] **Step 1: Write the failing service test**

```csharp
// Backup/Backup.Server.Tests/Integrity/ManualVerifyTests.cs
using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Backup.Server.Tests.Integrity;

public sealed class ManualVerifyTests
{
    private static readonly Guid ArtifactId = Guid.NewGuid();

    [Fact]
    public async Task Match_ReturnsVerified_AndUpdates()
    {
        var repo = new StubRepo(new BackupArtifact { Id = ArtifactId, ObjectKey = "k", FileName = "f", SizeBytes = 10, Checksum = "ABC" });
        var service = new BackupArtifactsService(repo, new StubStorage("abc"), new StubAudit(), Options.Create(new StorageOptions()));

        var result = await service.VerifyArtifactAsync(ArtifactId, actorId: null, CancellationToken.None);

        Assert.Equal(nameof(ArtifactIntegrityStatus.Verified), result.IntegrityStatus);
        Assert.Equal(ArtifactIntegrityStatus.Verified, repo.LastStatus);
        Assert.NotNull(repo.LastVerifiedAt);
    }

    [Fact]
    public async Task Mismatch_ReturnsFailed()
    {
        var repo = new StubRepo(new BackupArtifact { Id = ArtifactId, ObjectKey = "k", FileName = "f", SizeBytes = 10, Checksum = "expected" });
        var service = new BackupArtifactsService(repo, new StubStorage("different"), new StubAudit(), Options.Create(new StorageOptions()));

        var result = await service.VerifyArtifactAsync(ArtifactId, actorId: null, CancellationToken.None);

        Assert.Equal(nameof(ArtifactIntegrityStatus.Failed), result.IntegrityStatus);
        Assert.Equal(ArtifactIntegrityStatus.Failed, repo.LastStatus);
        Assert.Null(repo.LastVerifiedAt);
    }

    private sealed class StubStorage : IStorageAccessService
    {
        private readonly string _hash;
        public StubStorage(string hash) { _hash = hash; }
        public Task<string> ComputeObjectSha256Async(string objectKey, CancellationToken ct) => Task.FromResult(_hash);
        public Task<StorageObjectInfo> GetObjectInfoAsync(string objectKey, CancellationToken ct) => throw new NotImplementedException();
        public Task EnsureBucketExistsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<UploadTicketResponse> CreateUploadTicketAsync(Guid backupJobId, Guid policyId, Guid agentId, string fileName, string contentType, long sizeBytes, string? publicServerBaseUrl, CancellationToken ct) => throw new NotImplementedException();
        public Task WriteObjectToAsync(string objectKey, Stream destination, CancellationToken ct) => throw new NotImplementedException();
        public Task<string> CreateDownloadTicketAsync(string objectKey, long sizeBytes, string? publicServerBaseUrl, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteObjectAsync(string objectKey, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class StubRepo : IBackupArtifactRepository
    {
        private readonly BackupArtifact _artifact;
        public ArtifactIntegrityStatus? LastStatus { get; private set; }
        public DateTime? LastVerifiedAt { get; private set; }
        public StubRepo(BackupArtifact artifact) { _artifact = artifact; }
        public Task<BackupArtifact?> GetArtifactByIdAsync(Guid artifactId) => Task.FromResult<BackupArtifact?>(_artifact);
        public Task UpdateIntegrityAsync(Guid id, ArtifactIntegrityStatus status, DateTime? lastVerifiedAt, CancellationToken ct)
        { LastStatus = status; LastVerifiedAt = lastVerifiedAt; return Task.CompletedTask; }
        public Task<List<BackupArtifact>> GetAllArtifactsAsync() => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsByJobIdAsync(Guid jobId) => throw new NotImplementedException();
        public Task<int> CountByJobIdAsync(Guid jobId) => throw new NotImplementedException();
        public Task AddArtifact(BackupArtifact artifact) => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsForRetentionAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsForScrubAsync(int batchSize, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteArtifactAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChanges() => throw new NotImplementedException();
    }

    private sealed class StubAudit : IAuditLogRepository
    {
        public Task AddAsync(AuditLog log) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken ct) => throw new NotImplementedException();
    }
}
```

- [ ] **Step 2: Run it, expect FAIL (ctor/method missing)**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx --filter "FullyQualifiedName~ManualVerifyTests"`
Expected: compile failure — `BackupArtifactsService` has no 4-arg constructor / `VerifyArtifactAsync`.

- [ ] **Step 3: Create the result DTO**

```csharp
// Backup/Backup.Shared.Contracts/DTOs/Artifacts/ArtifactVerifyResultDto.cs
namespace Backup.Shared.Contracts.DTOs.Artifacts;

public record ArtifactVerifyResultDto(
    Guid Id,
    string IntegrityStatus,
    DateTime? LastVerifiedAt
);
```

- [ ] **Step 4: Extend BackupArtifactsService**

Replace the constructor and add the method. New usings: `Backup.Server.Domain.Enums;`, `Backup.Server.Infrastructure.Options;`, `Backup.Shared.Contracts.DTOs.Artifacts;`, `Microsoft.Extensions.Options;`.

```csharp
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly StorageOptions _storageOptions;

    public BackupArtifactsService(
        IBackupArtifactRepository backupArtifactRepository,
        IStorageAccessService storageAccessService,
        IAuditLogRepository auditLogRepository,
        IOptions<StorageOptions> storageOptions)
    {
        _backupArtifactRepository = backupArtifactRepository;
        _storageAccessService = storageAccessService;
        _auditLogRepository = auditLogRepository;
        _storageOptions = storageOptions.Value;
    }

    public async Task<ArtifactVerifyResultDto> VerifyArtifactAsync(Guid artifactId, Guid? actorId, CancellationToken cancellationToken)
    {
        var artifact = await _backupArtifactRepository.GetArtifactByIdAsync(artifactId)
            ?? throw new KeyNotFoundException($"Artifact with id {artifactId} does not exist");

        if (_storageOptions.ChecksumVerifyMaxBytes is not null
            && artifact.SizeBytes > _storageOptions.ChecksumVerifyMaxBytes.Value)
        {
            await _auditLogRepository.AddAsync(Audit("artifact.verify_skipped", actorId, artifact.Id,
                $"objectKey={artifact.ObjectKey} size={artifact.SizeBytes} limit={_storageOptions.ChecksumVerifyMaxBytes.Value}"));
            await _auditLogRepository.SaveChangesAsync();
            return new ArtifactVerifyResultDto(artifact.Id, artifact.IntegrityStatus.ToString(), artifact.LastVerifiedAt);
        }

        var computed = await _storageAccessService.ComputeObjectSha256Async(artifact.ObjectKey, cancellationToken);
        var match = string.Equals(computed, artifact.Checksum, StringComparison.OrdinalIgnoreCase);
        var status = match ? ArtifactIntegrityStatus.Verified : ArtifactIntegrityStatus.Failed;
        var verifiedAt = match ? (DateTime?)DateTime.UtcNow : null;

        await _backupArtifactRepository.UpdateIntegrityAsync(artifact.Id, status, verifiedAt, cancellationToken);
        await _auditLogRepository.AddAsync(Audit("artifact.verify_manual", actorId, artifact.Id,
            match ? $"objectKey={artifact.ObjectKey} result=verified"
                  : $"objectKey={artifact.ObjectKey} result=failed expected={artifact.Checksum} actual={computed}"));
        await _auditLogRepository.SaveChangesAsync();

        return new ArtifactVerifyResultDto(artifact.Id, status.ToString(), verifiedAt);
    }

    private static AuditLog Audit(string action, Guid? actorId, Guid targetId, string details) => new()
    {
        Id = Guid.NewGuid(),
        ActorId = actorId,
        Action = action,
        TargetId = targetId,
        Details = details,
        OccurredAt = DateTime.UtcNow,
    };
```

Add `using Backup.Server.Domain.Entities;` is already present; ensure `AuditLog` resolves (it's in `Backup.Server.Domain.Entities`).

- [ ] **Step 5: Run service tests, expect PASS**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx --filter "FullyQualifiedName~ManualVerifyTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Add the controller endpoint**

In `BackupArtifactsController.cs` add usings `Microsoft.AspNetCore.Authorization;` (present) and `Backup.Server.Api.Security;` (present). Add the action:

```csharp
    [HttpPost("{artifactId:guid}/verify")]
    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    public async Task<IActionResult> VerifyArtifact([FromRoute] Guid artifactId, CancellationToken cancellationToken)
    {
        var actorId = User.TryGetUserId();
        var result = await _backupArtifactsService.VerifyArtifactAsync(artifactId, actorId, cancellationToken);
        return Ok(result);
    }
```

> The class-level `[Authorize(AdminReadPolicy)]` is overridden by the method-level `AdminWritePolicy`.

- [ ] **Step 7: Build + full suite**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx`
Expected: all pass.

- [ ] **Step 8: Commit**

```bash
cd D:/projects/RestorMe
git add Backup/Backup.Server.Application Backup/Backup.Server.Api Backup/Backup.Shared.Contracts Backup/Backup.Server.Tests
git commit -m "feat(integrity): manual verify-now endpoint for artifacts"
```

---

### Task 6: Surface integrity state in the artifact DTO

**Files:**
- Modify: `Backup/Backup.Shared.Contracts/DTOs/Artifacts/BackupArtifactDto.cs`
- Modify: `Backup/Backup.Server.Api/Controllers/BackupArtifactsController.cs` (the `MapArtifact` helper)

**Interfaces:**
- Produces: `BackupArtifactDto` gains `string IntegrityStatus` and `DateTime? LastVerifiedAt` (appended last to preserve positional construction order of existing fields).

- [ ] **Step 1: Extend the DTO**

```csharp
using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Artifacts;

public record BackupArtifactDto(
    [Required] Guid Id,
    [Required] Guid JobId,
    [Required] string FileName,
    [Required] string ObjectKey,
    [Required] long Size,
    [Required] string Checksum,
    [Required] DateTime CreatedAt,
    [Required] string IntegrityStatus,
    DateTime? LastVerifiedAt
);
```

- [ ] **Step 2: Update the controller mapping**

In `BackupArtifactsController.MapArtifact`, append the two new arguments:
```csharp
    private static BackupArtifactDto MapArtifact(BackupArtifact artifact)
    {
        return new BackupArtifactDto(
            artifact.Id,
            artifact.JobId,
            artifact.FileName,
            artifact.ObjectKey,
            artifact.SizeBytes,
            artifact.Checksum,
            artifact.CreatedAt,
            artifact.IntegrityStatus.ToString(),
            artifact.LastVerifiedAt);
    }
```

- [ ] **Step 3: Build + full suite**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx`
Expected: build succeeds; all pass.

- [ ] **Step 4: Commit**

```bash
cd D:/projects/RestorMe
git add Backup/Backup.Shared.Contracts Backup/Backup.Server.Api
git commit -m "feat(integrity): expose integrity status on artifact DTO"
```

---

### Task 7: Verify-on-restore (backend DTO + agent)

**Files:**
- Modify: `Backup/Backup.Shared.Contracts/DTOs/Restore/PendingRestoreResponse.cs`
- Modify: `Backup/Backup.Server.Application/Services/RestoreJobsService.cs` (the `new PendingRestoreResponse(...)` at ~line 99)
- Modify: `Backup/Backup.Agent.Worker/Services/RestoreExecuter.cs` (inject `IChecksumService`, verify before apply)
- Create test: `Backup/Backup.Agent.Worker` has no test project; add an agent-focused test under `Backup/Backup.Server.Tests/Integrity/RestoreVerifyTests.cs` exercising the verification helper extracted from RestoreExecuter.

**Interfaces:**
- Produces: `PendingRestoreResponse.Checksum` (string?, appended last); `RestoreExecuter` now consumes `IChecksumService`.
- Consumes: agent `IChecksumService.ComputeSha256Async(filePath, ct)`.

- [ ] **Step 1: Add Checksum to the DTO (appended last to keep positional order)**

```csharp
using Backup.Shared.Contracts.DTOs.Policies;

namespace Backup.Shared.Contracts.DTOs.Restore;

public record PendingRestoreResponse(
    Guid JobId,
    string ObjectKey,
    string FileName,
    string PolicyType,
    string SourcePath,
    BackupPolicyDatabaseSettingsDto? DatabaseSettings,
    string? Checksum);
```

- [ ] **Step 2: Populate Checksum in RestoreJobsService**

At the `new PendingRestoreResponse(...)` (~line 99), append `artifact.Checksum` as the final argument:
```csharp
        return new PendingRestoreResponse(
            job.Id,
            artifact.ObjectKey,
            artifact.FileName,
            MapPolicyType(policy.Type),
            policy.SourcePath,
            MapDatabaseSettings(policy.DatabaseSettings),
            artifact.Checksum);
```

- [ ] **Step 3: Build backend to confirm DTO change compiles**

Run: `dotnet build D:/projects/RestorMe/Backup/BackupSystem.slnx --configuration Debug`
Expected: build succeeds (any other `new PendingRestoreResponse` callers would error — there are none besides this one).

- [ ] **Step 4: Write the failing verification-helper test**

Extract the decision into a static testable helper. First the test:
```csharp
// Backup/Backup.Server.Tests/Integrity/RestoreVerifyTests.cs
using Backup.Agent.Worker.Services;

namespace Backup.Server.Tests.Integrity;

public sealed class RestoreVerifyTests
{
    [Fact]
    public void EmptyExpected_AllowsRestore() // legacy artifact, no checksum
        => Assert.True(RestoreChecksumGate.ShouldProceed(expected: null, computed: "anything"));

    [Fact]
    public void Match_AllowsRestore()
        => Assert.True(RestoreChecksumGate.ShouldProceed(expected: "ABC", computed: "abc"));

    [Fact]
    public void Mismatch_BlocksRestore()
        => Assert.False(RestoreChecksumGate.ShouldProceed(expected: "abc", computed: "deadbeef"));
}
```

> The test project already references the agent worker project (it covers agent services). If `Backup.Agent.Worker` is not yet referenced by `Backup.Server.Tests`, add a `<ProjectReference>` to `Backup.Server.Tests.csproj` pointing at `..\Backup.Agent.Worker\Backup.Agent.Worker.csproj` as part of this step, then re-run.

- [ ] **Step 5: Run it, expect FAIL (helper missing)**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx --filter "FullyQualifiedName~RestoreVerifyTests"`
Expected: compile failure — `RestoreChecksumGate` does not exist.

- [ ] **Step 6: Implement the gate helper**

```csharp
// Backup/Backup.Agent.Worker/Services/RestoreChecksumGate.cs
namespace Backup.Agent.Worker.Services;

public static class RestoreChecksumGate
{
    // An empty/absent expected checksum means a legacy artifact uploaded before
    // checksums were recorded — proceed (backward compatible). Otherwise the
    // computed hash must match (case-insensitive).
    public static bool ShouldProceed(string? expected, string? computed)
    {
        if (string.IsNullOrWhiteSpace(expected))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(computed)
            && string.Equals(expected, computed, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 7: Run the gate test, expect PASS**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx --filter "FullyQualifiedName~RestoreVerifyTests"`
Expected: PASS (3 tests).

- [ ] **Step 8: Wire the gate into RestoreExecuter**

Inject `IChecksumService` and verify before `ApplyRestoreAsync`. Modify the constructor and `ExecutePendingAsync`:

Constructor — add field + parameter:
```csharp
    private readonly IChecksumService _checksumService;

    public RestoreExecuter(
        ILogger<RestoreExecuter> logger,
        IRestoreApiClient restoreApiClient,
        IMinioStorageClient storageClient,
        LogicalRestoreService logicalRestoreService,
        IChecksumService checksumService)
    {
        _logger = logger;
        _restoreApiClient = restoreApiClient;
        _storageClient = storageClient;
        _logicalRestoreService = logicalRestoreService;
        _checksumService = checksumService;
    }
```

In `ExecutePendingAsync`, between the download (`DownloadFileAsync`) and `ApplyRestoreAsync`:
```csharp
            await _storageClient.DownloadFileAsync(downloadUrl, tempFilePath, cancellationToken);

            var computed = await _checksumService.ComputeSha256Async(tempFilePath, cancellationToken);
            if (!RestoreChecksumGate.ShouldProceed(pending.Checksum, computed))
            {
                throw new InvalidOperationException(
                    "Artifact checksum verification failed: downloaded data does not match the expected SHA256. Restore target left untouched.");
            }
            if (string.IsNullOrWhiteSpace(pending.Checksum))
            {
                _logger.LogWarning("Restore job {JobId}: artifact has no recorded checksum; skipping integrity check", pending.JobId);
            }

            await ApplyRestoreAsync(pending.PolicyType, pending.SourcePath, pending.DatabaseSettings, tempFilePath, cancellationToken);
```

> The thrown exception is caught by the existing `catch` block, which calls `FailRestoreJobAsync` and rethrows — the target is never touched because the throw happens before `ApplyRestoreAsync`. `IChecksumService` is already registered in the agent `Program.cs`, so DI resolves the new ctor param automatically.

- [ ] **Step 9: Build + full suite**

Run: `dotnet test D:/projects/RestorMe/Backup/BackupSystem.slnx`
Expected: build succeeds; all pass.

- [ ] **Step 10: Commit**

```bash
cd D:/projects/RestorMe
git add Backup/Backup.Shared.Contracts Backup/Backup.Server.Application Backup/Backup.Agent.Worker Backup/Backup.Server.Tests
git commit -m "feat(integrity): verify artifact checksum before applying a restore"
```

---

### Task 8: Frontend — integrity badge + Verify now

**Files:**
- Modify: `Frontend-2.0/src/shared/api/artifacts.ts` (Artifact fields + `verifyArtifact` call)
- Modify: `Frontend-2.0/src/pages/artifacts/ArtifactsPage.tsx` (badge + action)
- Modify: `Frontend-2.0/src/shared/i18n/index.tsx` (en/ru keys)

**Interfaces:**
- Consumes: backend `BackupArtifactDto.integrityStatus` / `lastVerifiedAt`; `POST /api/backupartifacts/{id}/verify` → `{ id, integrityStatus, lastVerifiedAt }`.
- Produces: `verifyArtifact(id): Promise<ArtifactVerifyResult>`; UI badge + button.

- [ ] **Step 1: Extend the API client**

In `artifacts.ts`, add fields to the `Artifact` interface:
```ts
  integrityStatus?: 'Unverified' | 'Verified' | 'Failed'
  lastVerifiedAt?: string | null
```
And add the call + result type:
```ts
export interface ArtifactVerifyResult {
  id: string
  integrityStatus: 'Unverified' | 'Verified' | 'Failed'
  lastVerifiedAt?: string | null
}

export async function verifyArtifact(artifactId: string): Promise<ArtifactVerifyResult> {
  const response = await apiClient.post(`/api/backupartifacts/${artifactId}/verify`)
  return response.data
}
```

- [ ] **Step 2: Add a verify mutation + integrity badge in ArtifactsPage**

In `ArtifactsPage.tsx`:

Add imports: `ShieldCheck, ShieldAlert, ShieldQuestion` from `lucide-react`, and `verifyArtifact` from the api module; ensure `useQueryClient` is imported from `@tanstack/react-query`.

Add a verify mutation inside `ArtifactsPage` (next to `downloadMutation`):
```tsx
  const queryClient = useQueryClient()
  const verifyMutation = useMutation({
    mutationFn: (artifact: Artifact) => verifyArtifact(artifact.id),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.artifacts })
      if (result.integrityStatus === 'Verified') {
        toast.success(t('Integrity verified'))
      } else if (result.integrityStatus === 'Failed') {
        toast.error(t('Integrity check failed'))
      } else {
        toast.message(t('Integrity check skipped'))
      }
    },
    onError: (error) => {
      toast.error(error instanceof Error ? error.message : t('Integrity check failed'))
    },
  })
```

Pass it down to `ArtifactRow`:
```tsx
                  onVerify={() => verifyMutation.mutate(artifact)}
                  isVerifying={verifyMutation.isPending && verifyMutation.variables?.id === artifact.id}
```
Add `onVerify: () => void` and `isVerifying: boolean` to the `ArtifactRow` prop type.

Add a small badge component near the other helpers:
```tsx
function IntegrityBadge({ status, t }: { status?: string; t: (k: string) => string }) {
  if (status === 'Verified') {
    return <Badge variant="accent"><ShieldCheck className="mr-1 h-3 w-3" />{t('Verified')}</Badge>
  }
  if (status === 'Failed') {
    return <Badge variant="neutral" className="text-warning"><ShieldAlert className="mr-1 h-3 w-3" />{t('Corrupt')}</Badge>
  }
  return <Badge variant="neutral"><ShieldQuestion className="mr-1 h-3 w-3" />{t('Unverified')}</Badge>
}
```
> If `Badge` does not accept a `className` prop, drop the `className` and keep the icon+label only. Confirm against `@/shared/ui/Badge`.

Render the badge next to the type badge (inside the name block, after the existing `<Badge>`):
```tsx
            <IntegrityBadge status={artifact.integrityStatus} t={t} />
```

Add a "Verify now" button in the actions area (before the Restore button):
```tsx
        <Button variant="secondary" size="sm" onClick={onVerify} disabled={isVerifying} title={t('Re-hash the stored object and compare to its recorded checksum')}>
          {isVerifying ? t('Verifying...') : t('Verify now')}
        </Button>
```

- [ ] **Step 3: Add i18n keys**

In `Frontend-2.0/src/shared/i18n/index.tsx`, add Russian translations for the new keys to the `ru` dictionary (the `en` default is the key itself):
```ts
  'Verify now': 'Проверить',
  'Verifying...': 'Проверка...',
  'Verified': 'Цел',
  'Corrupt': 'Повреждён',
  'Unverified': 'Не проверен',
  'Integrity verified': 'Целостность подтверждена',
  'Integrity check failed': 'Проверка целостности не пройдена',
  'Integrity check skipped': 'Проверка пропущена (объект слишком большой)',
  'Re-hash the stored object and compare to its recorded checksum': 'Перечитать объект из хранилища и сверить SHA256 с сохранённым',
```
> Match the existing dictionary's exact shape/indentation in that file.

- [ ] **Step 4: Typecheck + lint**

Run:
```bash
cd D:/projects/RestorMe/Frontend-2.0
yarn typecheck && yarn lint
```
Expected: no type errors, no lint errors.

- [ ] **Step 5: Commit**

```bash
cd D:/projects/RestorMe
git add Frontend-2.0
git commit -m "feat(integrity): artifact integrity badge + verify-now action"
```

---

## Self-Review

**Spec coverage:**
- Data model (IntegrityStatus + LastVerifiedAt + migration) → Task 1. ✓
- Scrub sweep (service, options, prioritised batch, size cap, audit, notification) → Tasks 3 + 4. ✓
- `IntegrityCheckFailed` notification event → Task 2. ✓
- Verify-on-restore (DTO Checksum + agent gate, target untouched, legacy skip) → Task 7. ✓
- Minimal UI (badge + Verify now + endpoint + i18n) → Tasks 5, 6, 8. ✓
- Testing (pure decision unit, scrub integration, manual-verify, restore gate) → Tasks 3, 4, 5, 7. ✓
- Config additions (`Integrity` section) → Task 4 Step 6. ✓

**Placeholder scan:** No TBD/TODO; every code step shows full code. Two explicit "confirm against existing code" notes (Badge className, seed property names) are guardrails, not placeholders — the fallback behavior is specified.

**Type consistency:** `ArtifactIntegrityStatus` (Task 1) used consistently in repo (Task 3), service (Task 4), manual verify (Task 5), DTO mapping (Task 6). `ScrubOutcome`/`IntegrityScrubDecision.Evaluate` signature identical across Tasks 3 and 4. `GetArtifactsForScrubAsync(int, CancellationToken)` and `UpdateIntegrityAsync(Guid, ArtifactIntegrityStatus, DateTime?, CancellationToken)` identical in interface (Task 3), impl (Task 3), and all test stubs (Tasks 3, 4, 5). `NotifyIntegrityCheckFailedAsync(int, CancellationToken)` identical across interface, dispatcher, and stubs (Tasks 2, 4). `PendingRestoreResponse.Checksum` appended last and populated (Task 7). `RestoreChecksumGate.ShouldProceed(string?, string?)` identical in test and impl (Task 7).

**Known follow-ups (out of scope, noted in spec):** verified restore-drills; at-rest encryption; replication-based auto-remediation of Failed artifacts.
