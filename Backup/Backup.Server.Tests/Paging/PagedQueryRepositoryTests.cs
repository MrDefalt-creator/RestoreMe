using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Tests.Paging;

/// <summary>
/// Integration tests for the paged admin list queries (jobs / artifacts /
/// agents). SQLite in-memory so ordering + Skip/Take run through a real
/// query provider.
/// </summary>
public sealed class PagedQueryRepositoryTests : IAsyncLifetime
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
        Seed(ctx);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    private AppDbContext NewContext() => new(_options, _dataProtection);

    private static readonly DateTime Base = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);

    private static void Seed(AppDbContext ctx)
    {
        var agents = Enumerable.Range(0, 5).Select(i => new Agent
        {
            Id = Guid.NewGuid(),
            Name = $"agent-{(char)('e' - i)}", // names e,d,c,b,a — reverse of CreatedAt order
            MachineName = $"machine-{i}",
            OsType = "linux",
            Version = "1.0",
            Status = i % 2 == 0 ? AgentStatus.Online : AgentStatus.Offline,
            CreatedAt = Base.AddMinutes(i),
            LastSeenAt = Base.AddMinutes(10 + i),
        }).ToList();
        ctx.Agents.AddRange(agents);

        var policy = new BackupPolicy
        {
            Id = Guid.NewGuid(),
            AgentId = agents[0].Id,
            Type = BackupPolicyType.FileSystem,
            Name = "Nightly",
            SourcePath = "/etc",
            IntervalSeconds = 3600,
            NextRunAt = Base,
        };
        ctx.BackupPolicies.Add(policy);

        var jobs = Enumerable.Range(0, 7).Select(i => new BackupJob
        {
            Id = Guid.NewGuid(),
            AgentId = agents[0].Id,
            PolicyId = policy.Id,
            Status = i % 3 == 0 ? BackupJobStatus.Failed : BackupJobStatus.Completed,
            StartedAt = Base.AddMinutes(i),
            CompletedAt = Base.AddMinutes(i + 1),
        }).ToList();
        ctx.BackupJobs.AddRange(jobs);

        var artifacts = Enumerable.Range(0, 7).Select(i => new BackupArtifact
        {
            Id = Guid.NewGuid(),
            JobId = jobs[i].Id,
            FileName = $"backup-{i}.zip",
            ObjectKey = $"backups/backup-{i}.zip",
            SizeBytes = 1000 - i * 100, // newest is the smallest
            Checksum = "deadbeef",
            CreatedAt = Base.AddMinutes(i),
        }).ToList();
        ctx.BackupArtifacts.AddRange(artifacts);

        ctx.SaveChanges();
    }

    [Fact]
    public async Task Jobs_paged_default_sort_is_startedAt_desc_with_total()
    {
        await using var ctx = NewContext();
        var repo = new BackupJobRepository(ctx);

        var result = await repo.QueryBackupJobsAsync(
            new PagedQuery(1, 3, null, SortDescending: true), CancellationToken.None);

        Assert.Equal(7, result.Total);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(Base.AddMinutes(6), result.Items[0].StartedAt);
        Assert.Equal(Base.AddMinutes(4), result.Items[2].StartedAt);
    }

    [Fact]
    public async Task Jobs_second_page_continues_where_first_ended()
    {
        await using var ctx = NewContext();
        var repo = new BackupJobRepository(ctx);

        var page2 = await repo.QueryBackupJobsAsync(
            new PagedQuery(2, 3, null, SortDescending: true), CancellationToken.None);

        Assert.Equal(7, page2.Total);
        Assert.Equal(3, page2.Items.Count);
        Assert.Equal(Base.AddMinutes(3), page2.Items[0].StartedAt);
    }

    [Fact]
    public async Task Jobs_unknown_sort_key_falls_back_to_startedAt()
    {
        await using var ctx = NewContext();
        var repo = new BackupJobRepository(ctx);

        var result = await repo.QueryBackupJobsAsync(
            new PagedQuery(1, 2, "definitely-not-a-column", SortDescending: false), CancellationToken.None);

        Assert.Equal(Base, result.Items[0].StartedAt);
    }

    [Fact]
    public async Task Artifacts_sort_by_size_ascending()
    {
        await using var ctx = NewContext();
        var repo = new BackupArtifactRepository(ctx);

        var result = await repo.QueryArtifactsAsync(
            new PagedQuery(1, 3, "size", SortDescending: false), CancellationToken.None);

        Assert.Equal(7, result.Total);
        Assert.Equal(400, result.Items[0].SizeBytes);
        Assert.True(result.Items[0].SizeBytes <= result.Items[1].SizeBytes);
    }

    [Fact]
    public async Task Agents_sort_by_name_ascending_beats_createdAt_order()
    {
        await using var ctx = NewContext();
        var repo = new AgentRepository(ctx);

        var result = await repo.QueryAgentsAsync(
            new PagedQuery(1, 10, "name", SortDescending: false), CancellationToken.None);

        Assert.Equal(5, result.Total);
        Assert.Equal("agent-a", result.Items[0].Name);
        Assert.Equal("agent-e", result.Items[^1].Name);
    }

    [Fact]
    public async Task Page_past_the_end_returns_empty_items_but_keeps_total()
    {
        await using var ctx = NewContext();
        var repo = new AgentRepository(ctx);

        var result = await repo.QueryAgentsAsync(
            new PagedQuery(10, 50, null, SortDescending: true), CancellationToken.None);

        Assert.Equal(5, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public void Normalize_clamps_page_size_and_direction()
    {
        var q = PagedQuery.Normalize(page: 0, pageSize: 1000, sortBy: "name", sortDir: "ASC");
        Assert.Equal(1, q.Page);
        Assert.Equal(200, q.PageSize);
        Assert.False(q.SortDescending);

        var defaults = PagedQuery.Normalize(null, null, null, null);
        Assert.Equal(1, defaults.Page);
        Assert.Equal(50, defaults.PageSize);
        Assert.True(defaults.SortDescending);
    }
}
