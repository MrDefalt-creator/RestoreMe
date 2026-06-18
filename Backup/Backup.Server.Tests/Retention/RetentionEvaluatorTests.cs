using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;

namespace Backup.Server.Tests.Retention;

/// <summary>
/// Unit tests for <see cref="RetentionEvaluator"/> — the pure, DB-free logic
/// that decides which artifacts a policy's retention rules prune. The newest
/// artifact of a policy is always preserved (floor), keep-rules (days/count)
/// union, and the size cap prunes oldest-first beyond the byte budget.
/// </summary>
public sealed class RetentionEvaluatorTests
{
    private static readonly DateTime Now = new(2026, 6, 18, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NoRetentionRules_DeletesNothing()
    {
        var policy = NewPolicy();
        var artifacts = Build(policy, (ageDays: 0, size: 10), (ageDays: 100, size: 10));

        var result = RetentionEvaluator.SelectForDeletion(artifacts, Now);

        Assert.Empty(result);
    }

    [Fact]
    public void MaxCount_KeepsNewestN_PrunesRest()
    {
        var policy = NewPolicy();
        policy.RetentionMaxCount = 2;
        var artifacts = Build(policy,
            (ageDays: 0, size: 10),   // newest, kept (floor + within N)
            (ageDays: 1, size: 10),   // within N, kept
            (ageDays: 2, size: 10),   // rank 2 -> pruned
            (ageDays: 3, size: 10));  // rank 3 -> pruned

        var result = RetentionEvaluator.SelectForDeletion(artifacts, Now);

        Assert.Equal(2, result.Count);
        Assert.All(result, d => Assert.Equal(RetentionReason.Count, d.Reason));
    }

    [Fact]
    public void RetentionDays_PrunesOlder_ButAlwaysKeepsNewest()
    {
        var policy = NewPolicy();
        policy.RetentionDays = 7;
        var artifacts = Build(policy,
            (ageDays: 30, size: 10),  // newest of this set is still 30d old
            (ageDays: 40, size: 10),
            (ageDays: 50, size: 10));

        var result = RetentionEvaluator.SelectForDeletion(artifacts, Now);

        // Floor protects the newest (30d) even though it's past retention; the
        // two older ones are pruned by age.
        Assert.Equal(2, result.Count);
        Assert.All(result, d => Assert.Equal(RetentionReason.Age, d.Reason));
        Assert.DoesNotContain(result, d => d.Artifact.CreatedAt == Now.AddDays(-30));
    }

    [Fact]
    public void DaysOrCount_KeepUnion_ProtectsIfEither()
    {
        var policy = NewPolicy();
        policy.RetentionDays = 1;     // only the newest is within a day
        policy.RetentionMaxCount = 3; // but keep-last-3 protects the next two
        var artifacts = Build(policy,
            (ageDays: 0, size: 10),
            (ageDays: 5, size: 10),   // protected by count (rank 1 < 3)
            (ageDays: 6, size: 10),   // protected by count (rank 2 < 3)
            (ageDays: 7, size: 10));  // rank 3 -> pruned

        var result = RetentionEvaluator.SelectForDeletion(artifacts, Now);

        Assert.Single(result);
        Assert.Equal(Now.AddDays(-7), result[0].Artifact.CreatedAt);
    }

    [Fact]
    public void MaxTotalBytes_PrunesOldestBeyondBudget_KeepsNewest()
    {
        var policy = NewPolicy();
        policy.RetentionMaxTotalBytes = 25;
        var artifacts = Build(policy,
            (ageDays: 0, size: 10),   // cumulative 10 <= 25, kept
            (ageDays: 1, size: 10),   // cumulative 20 <= 25, kept
            (ageDays: 2, size: 10));  // cumulative 30 > 25, pruned (size)

        var result = RetentionEvaluator.SelectForDeletion(artifacts, Now);

        Assert.Single(result);
        Assert.Equal(RetentionReason.Size, result[0].Reason);
        Assert.Equal(Now.AddDays(-2), result[0].Artifact.CreatedAt);
    }

    [Fact]
    public void SizeCap_NeverPrunesNewest_EvenIfOverBudget()
    {
        var policy = NewPolicy();
        policy.RetentionMaxTotalBytes = 5; // single artifact already exceeds it
        var artifacts = Build(policy, (ageDays: 0, size: 100));

        var result = RetentionEvaluator.SelectForDeletion(artifacts, Now);

        Assert.Empty(result);
    }

    private static BackupPolicy NewPolicy() => new()
    {
        Id = Guid.NewGuid(),
        AgentId = Guid.NewGuid(),
        Name = "test",
        SourcePath = "/data",
    };

    private static List<BackupArtifact> Build(BackupPolicy policy, params (int ageDays, long size)[] specs)
    {
        var job = new BackupJob { Id = Guid.NewGuid(), PolicyId = policy.Id, Policy = policy };
        return specs.Select(s => new BackupArtifact
        {
            Id = Guid.NewGuid(),
            FileName = "backup.zip",
            ObjectKey = Guid.NewGuid().ToString(),
            Checksum = "deadbeef",
            SizeBytes = s.size,
            CreatedAt = Now.AddDays(-s.ageDays),
            JobId = job.Id,
            Job = job,
        }).ToList();
    }
}
