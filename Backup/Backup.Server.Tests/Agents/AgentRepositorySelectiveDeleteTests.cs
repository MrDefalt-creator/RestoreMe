using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Services;
using Backup.Shared.Contracts.DTOs.Agents;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Tests.Agents;

/// <summary>
/// Integration tests for selective agent delete (Group H of the
/// 2026-05-26 polish PR). Uses SQLite in-memory so cascade behaviour
/// configured in BackupJobConfiguration / RestoreJobConfiguration
/// actually fires; EF InMemory would just ignore the FKs.
/// </summary>
public sealed class AgentRepositorySelectiveDeleteTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<AppDbContext> _options = null!;
    private IDataProtectionProvider _dataProtection = null!;

    public Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dataProtection = new EphemeralDataProtectionProvider();

        using var ctx = NewContext();
        ctx.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    private AppDbContext NewContext() => new(_options, _dataProtection);

    private static (Guid agentId, Guid policyId, Guid jobId, Guid artifactId, Guid restoreId) SeedFullChain(AppDbContext ctx)
    {
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = "Primary host",
            MachineName = "db-primary-01",
            OsType = "linux",
            Version = "1.0",
            Status = AgentStatus.Online,
            CreatedAt = DateTime.UtcNow,
        };
        var policy = new BackupPolicy
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            Type = BackupPolicyType.FileSystem,
            Name = "Nightly /etc",
            SourcePath = "/etc",
            IntervalSeconds = 3600,
            NextRunAt = DateTime.UtcNow,
        };
        var job = new BackupJob
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            PolicyId = policy.Id,
            Status = BackupJobStatus.Completed,
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
        };
        var artifact = new BackupArtifact
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            FileName = "etc.tar.zst",
            ObjectKey = $"{agent.Id}/{policy.Id}/{job.Id}/etc.tar.zst",
            SizeBytes = 12_345,
            Checksum = "sha256:fake",
        };
        var restore = new RestoreJob
        {
            Id = Guid.NewGuid(),
            ArtifactId = artifact.Id,
            AgentId = agent.Id,
            Status = RestoreJobStatus.Completed,
            CreatedAt = DateTime.UtcNow.AddMinutes(-30),
            CompletedAt = DateTime.UtcNow.AddMinutes(-10),
        };

        ctx.Agents.Add(agent);
        ctx.BackupPolicies.Add(policy);
        ctx.BackupJobs.Add(job);
        ctx.BackupArtifacts.Add(artifact);
        ctx.RestoreJobs.Add(restore);
        ctx.SaveChanges();

        return (agent.Id, policy.Id, job.Id, artifact.Id, restore.Id);
    }

    [Fact]
    public async Task PurgeAll_RemovesEverything_AndReturnsStorageKeys()
    {
        Guid agentId;
        Guid artifactId;
        using (var seed = NewContext())
        {
            var ids = SeedFullChain(seed);
            agentId = ids.agentId;
            artifactId = ids.artifactId;
        }

        List<string> storageKeys;
        await using (var ctx = NewContext())
        {
            var repo = new AgentRepository(ctx);
            var options = new DeleteAgentOptions(true, true, true);
            storageKeys = await repo.DeleteAgentAsync(agentId, options, CancellationToken.None);
        }

        await using var verify = NewContext();
        Assert.Empty(verify.Agents);
        Assert.Empty(verify.BackupPolicies);
        Assert.Empty(verify.BackupJobs);
        Assert.Empty(verify.BackupArtifacts);
        Assert.Empty(verify.RestoreJobs);
        Assert.Single(storageKeys);
        Assert.Contains(agentId.ToString(), storageKeys[0]);
        Assert.Contains(artifactId.ToString()[..0], storageKeys[0]); // sanity check on shape
    }

    [Fact]
    public async Task KeepBackupHistory_NullifiesFKsAndSnapshotsName()
    {
        Guid agentId;
        Guid jobId;
        using (var seed = NewContext())
        {
            var ids = SeedFullChain(seed);
            agentId = ids.agentId;
            jobId = ids.jobId;
        }

        await using (var ctx = NewContext())
        {
            var repo = new AgentRepository(ctx);
            var options = new DeleteAgentOptions(
                PurgeBackupHistory: false,
                PurgeStorageFiles: false,
                PurgeRestoreHistory: true);
            var storageKeys = await repo.DeleteAgentAsync(agentId, options, CancellationToken.None);
            Assert.Empty(storageKeys);
        }

        await using var verify = NewContext();
        Assert.Empty(verify.Agents);
        var job = await verify.BackupJobs.SingleAsync(j => j.Id == jobId);
        Assert.Null(job.AgentId);
        Assert.Null(job.PolicyId);
        Assert.Equal("Primary host", job.AgentNameSnapshot);
        Assert.Equal("Nightly /etc", job.PolicyNameSnapshot);
        // Artifacts stay because Job stays.
        Assert.Single(verify.BackupArtifacts);
    }

    [Fact]
    public async Task PendingRestore_BlocksDelete_UnlessPurgeRestoresIsOn()
    {
        Guid agentId;
        using (var seed = NewContext())
        {
            var ids = SeedFullChain(seed);
            agentId = ids.agentId;
            // Flip the restore back to running.
            var restore = await seed.RestoreJobs.SingleAsync(r => r.Id == ids.restoreId);
            restore.Status = RestoreJobStatus.Running;
            await seed.SaveChangesAsync();
        }

        await using (var ctx = NewContext())
        {
            var repo = new AgentRepository(ctx);
            var options = new DeleteAgentOptions(
                PurgeBackupHistory: true,
                PurgeStorageFiles: true,
                PurgeRestoreHistory: false);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => repo.DeleteAgentAsync(agentId, options, CancellationToken.None));
            Assert.Contains("Pending or running restore jobs", ex.Message);
        }

        // Now allow restore purge and confirm it goes through.
        await using (var ctx = NewContext())
        {
            var repo = new AgentRepository(ctx);
            var ok = new DeleteAgentOptions(true, true, true);
            await repo.DeleteAgentAsync(agentId, ok, CancellationToken.None);
        }

        await using var verify = NewContext();
        Assert.Empty(verify.Agents);
        Assert.Empty(verify.RestoreJobs);
    }

    [Fact]
    public async Task KeepStorageFiles_SkipsObjectKeyCollection()
    {
        Guid agentId;
        using (var seed = NewContext())
        {
            var ids = SeedFullChain(seed);
            agentId = ids.agentId;
        }

        List<string> keys;
        await using (var ctx = NewContext())
        {
            var repo = new AgentRepository(ctx);
            var options = new DeleteAgentOptions(
                PurgeBackupHistory: true,
                PurgeStorageFiles: false,
                PurgeRestoreHistory: true);
            keys = await repo.DeleteAgentAsync(agentId, options, CancellationToken.None);
        }

        Assert.Empty(keys);

        await using var verify = NewContext();
        Assert.Empty(verify.Agents);
        Assert.Empty(verify.BackupJobs);
        Assert.Empty(verify.BackupArtifacts);
    }

    [Fact]
    public async Task KeepRestoreHistory_PreservesCrossAgentRestoreFKs()
    {
        // Two agents: Y owns the backup, X executes a restore of Y's
        // artifact. Deleting Y with "keep restore history" must not
        // touch the restore row's AgentId (it's X, not Y).
        Guid agentYId;
        Guid agentXId;
        Guid crossRestoreId;
        using (var seed = NewContext())
        {
            var ids = SeedFullChain(seed);
            agentYId = ids.agentId;

            var agentX = new Agent
            {
                Id = Guid.NewGuid(),
                Name = "Other host",
                MachineName = "ops-02",
                OsType = "linux",
                Version = "1.0",
                Status = AgentStatus.Online,
                CreatedAt = DateTime.UtcNow,
            };
            seed.Agents.Add(agentX);

            var seedCrossRestore = new RestoreJob
            {
                Id = Guid.NewGuid(),
                ArtifactId = ids.artifactId,
                AgentId = agentX.Id,
                Status = RestoreJobStatus.Completed,
                CreatedAt = DateTime.UtcNow.AddMinutes(-5),
                CompletedAt = DateTime.UtcNow.AddMinutes(-1),
            };
            seed.RestoreJobs.Add(seedCrossRestore);
            await seed.SaveChangesAsync();
            agentXId = agentX.Id;
            crossRestoreId = seedCrossRestore.Id;
        }

        await using (var ctx = NewContext())
        {
            var repo = new AgentRepository(ctx);
            var options = new DeleteAgentOptions(
                PurgeBackupHistory: true,
                PurgeStorageFiles: true,
                PurgeRestoreHistory: false);
            await repo.DeleteAgentAsync(agentYId, options, CancellationToken.None);
        }

        await using var verify = NewContext();
        var crossRestore = await verify.RestoreJobs.SingleAsync(r => r.Id == crossRestoreId);
        // AgentId must still point at X — the deletion of Y has no
        // authority to rewrite that row's executor.
        Assert.Equal(agentXId, crossRestore.AgentId);
        Assert.Null(crossRestore.AgentNameSnapshot);
        // Artifact got cascade-deleted with Y, so the artifact FK is
        // null but the display strings come from the snapshot.
        Assert.Null(crossRestore.ArtifactId);
        Assert.Equal("etc.tar.zst", crossRestore.ArtifactFileNameSnapshot);
        Assert.NotNull(crossRestore.ArtifactObjectKeySnapshot);
    }

    [Fact]
    public async Task DeletionImpact_CountsEverythingAttachedToAgent()
    {
        Guid agentId;
        using (var seed = NewContext())
        {
            var ids = SeedFullChain(seed);
            agentId = ids.agentId;
            // Add a second pending restore so the pending counter > 0.
            seed.RestoreJobs.Add(new RestoreJob
            {
                Id = Guid.NewGuid(),
                ArtifactId = ids.artifactId,
                AgentId = ids.agentId,
                Status = RestoreJobStatus.Pending,
                CreatedAt = DateTime.UtcNow,
            });
            await seed.SaveChangesAsync();
        }

        await using var ctx = NewContext();
        var repo = new AgentRepository(ctx);
        var impact = await repo.GetDeletionImpactAsync(agentId, CancellationToken.None);

        Assert.Equal(1, impact.PolicyCount);
        Assert.Equal(1, impact.BackupJobCount);
        Assert.Equal(1, impact.ArtifactCount);
        Assert.Equal(12_345, impact.TotalStorageBytes);
        Assert.Equal(2, impact.RestoreJobCount);
        Assert.Equal(1, impact.PendingRestoreJobCount);
    }
}
