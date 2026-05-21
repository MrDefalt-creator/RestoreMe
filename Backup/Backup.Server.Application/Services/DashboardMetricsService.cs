using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs.Dashboard;

namespace Backup.Server.Application.Services;

public class DashboardMetricsService
{
    private const int TopFailingLimit = 5;

    private readonly IBackupJobRepository _jobs;
    private readonly IBackupArtifactRepository _artifacts;
    private readonly IPolicyRepository _policies;

    public DashboardMetricsService(
        IBackupJobRepository jobs,
        IBackupArtifactRepository artifacts,
        IPolicyRepository policies)
    {
        _jobs = jobs;
        _artifacts = artifacts;
        _policies = policies;
    }

    /// <summary>
    /// Builds the aggregated dashboard payload for the given lookback window
    /// (in days). All collections are returned ordered from oldest to newest
    /// so the frontend can hand them straight to a chart without a re-sort.
    /// </summary>
    public async Task<DashboardMetricsDto> GetMetricsAsync(int periodDays)
    {
        if (periodDays <= 0) throw new ArgumentOutOfRangeException(nameof(periodDays));

        // [from, to) covers a whole number of UTC days ending today (inclusive).
        var to = DateTime.UtcNow.Date.AddDays(1);
        var from = to.AddDays(-periodDays);

        // The dataset is small for a self-hosted backend (one operator, dozens
        // to low thousands of rows). In-memory aggregation keeps the SQL
        // surface trivial — we hit each table once.
        var allJobs = await _jobs.GetAllBackupJobsAsync();
        var allArtifacts = await _artifacts.GetAllArtifactsAsync();
        var allPolicies = await _policies.GetAllPoliciesAsync();

        var days = Enumerable.Range(0, periodDays)
            .Select(offset => from.AddDays(offset))
            .ToList();

        var successRate = BuildSuccessRate(allJobs, days, to);
        var storageGrowth = BuildStorageGrowth(allArtifacts, days, from);
        var topFailing = BuildTopFailing(allJobs, allPolicies, from, to);
        var engineBreakdown = BuildEngineBreakdown(allPolicies);

        return new DashboardMetricsDto(
            $"{periodDays}d",
            from,
            to,
            successRate,
            storageGrowth,
            topFailing,
            engineBreakdown);
    }

    private static IReadOnlyList<SuccessRatePointDto> BuildSuccessRate(
        IReadOnlyList<Domain.Entities.BackupJob> jobs,
        IReadOnlyList<DateTime> days,
        DateTime windowEndExclusive)
    {
        var windowStart = days[0];

        // Pre-zero every day in the window so quiet days still show up as
        // bars/points on the chart instead of being skipped.
        var byDay = days.ToDictionary(d => d, _ => (Completed: 0, Failed: 0));

        foreach (var job in jobs)
        {
            if (job.StartedAt < windowStart || job.StartedAt >= windowEndExclusive)
            {
                continue;
            }

            var day = job.StartedAt.Date;
            if (!byDay.TryGetValue(day, out var counts))
            {
                continue;
            }

            if (job.Status == BackupJobStatus.Completed)
            {
                byDay[day] = (counts.Completed + 1, counts.Failed);
            }
            else if (job.Status == BackupJobStatus.Failed)
            {
                byDay[day] = (counts.Completed, counts.Failed + 1);
            }
        }

        return days
            .Select(d =>
            {
                var counts = byDay[d];
                return new SuccessRatePointDto(d.ToString("yyyy-MM-dd"), counts.Completed, counts.Failed);
            })
            .ToList();
    }

    private static IReadOnlyList<StorageGrowthPointDto> BuildStorageGrowth(
        IReadOnlyList<Domain.Entities.BackupArtifact> artifacts,
        IReadOnlyList<DateTime> days,
        DateTime windowStart)
    {
        // The window shows growth, but it has to start from the baseline that
        // existed before the lookback began — otherwise day-1 of a 7-day view
        // would look like the cluster size is brand new.
        var cumulative = artifacts.Where(a => a.CreatedAt < windowStart).Sum(a => a.SizeBytes);

        var inRange = artifacts
            .Where(a => a.CreatedAt >= windowStart)
            .OrderBy(a => a.CreatedAt)
            .ToList();

        var points = new List<StorageGrowthPointDto>(days.Count);
        var idx = 0;
        foreach (var day in days)
        {
            var endOfDayExclusive = day.AddDays(1);
            while (idx < inRange.Count && inRange[idx].CreatedAt < endOfDayExclusive)
            {
                cumulative += inRange[idx].SizeBytes;
                idx++;
            }

            points.Add(new StorageGrowthPointDto(day.ToString("yyyy-MM-dd"), cumulative));
        }

        return points;
    }

    private static IReadOnlyList<TopFailingPolicyDto> BuildTopFailing(
        IReadOnlyList<Domain.Entities.BackupJob> jobs,
        IReadOnlyList<Domain.Entities.BackupPolicy> policies,
        DateTime windowStart,
        DateTime windowEndExclusive)
    {
        var policyNames = policies.ToDictionary(p => p.Id, p => p.Name);

        return jobs
            .Where(j => j.StartedAt >= windowStart
                && j.StartedAt < windowEndExclusive
                && j.Status == BackupJobStatus.Failed)
            .GroupBy(j => j.PolicyId)
            .Select(g => new TopFailingPolicyDto(
                g.Key,
                policyNames.TryGetValue(g.Key, out var name) ? name : string.Empty,
                g.Count()))
            .OrderByDescending(t => t.FailureCount)
            .ThenBy(t => t.PolicyName)
            .Take(TopFailingLimit)
            .ToList();
    }

    private static IReadOnlyList<EngineBreakdownDto> BuildEngineBreakdown(
        IReadOnlyList<Domain.Entities.BackupPolicy> policies)
    {
        // Map domain enum names to the friendlier identifiers the frontend
        // already uses for filtering ('filesystem' / 'postgres' / 'mysql').
        // Keeps recharts donut/legend labels aligned with the rest of the UI.
        static string ToEngineKey(BackupPolicyType type) => type switch
        {
            BackupPolicyType.FileSystem => "filesystem",
            BackupPolicyType.PostgreSqlDump => "postgres",
            BackupPolicyType.MySqlDump => "mysql",
            _ => type.ToString().ToLowerInvariant(),
        };

        return policies
            .GroupBy(p => ToEngineKey(p.Type))
            .Select(g => new EngineBreakdownDto(g.Key, g.Count()))
            .OrderByDescending(e => e.PolicyCount)
            .ThenBy(e => e.Engine)
            .ToList();
    }
}
