using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs.Dashboard;

namespace Backup.Server.Application.Services;

public class DashboardSummaryService
{
    private const int RecentLimit = 5;
    private const int Last7Days = 7;

    private readonly IAgentRepository _agents;
    private readonly IPendingAgentsRepository _pendingAgents;
    private readonly IPolicyRepository _policies;
    private readonly IBackupJobRepository _jobs;
    private readonly IBackupArtifactRepository _artifacts;
    private readonly AgentService _agentService;

    public DashboardSummaryService(
        IAgentRepository agents,
        IPendingAgentsRepository pendingAgents,
        IPolicyRepository policies,
        IBackupJobRepository jobs,
        IBackupArtifactRepository artifacts,
        AgentService agentService)
    {
        _agents = agents;
        _pendingAgents = pendingAgents;
        _policies = policies;
        _jobs = jobs;
        _artifacts = artifacts;
        _agentService = agentService;
    }

    /// <summary>
    /// Builds the dashboard summary payload — instant counts, the 7-day
    /// job-volume strip, the unresolved-failure attention list and the
    /// "latest activity" preview. Replaces the previous frontend pattern
    /// of fetching every list endpoint and aggregating client-side.
    /// </summary>
    public async Task<DashboardSummaryDto> GetSummaryAsync()
    {
        // Same in-memory aggregation pattern as DashboardMetricsService —
        // the dataset is small for a self-hosted backend and keeping the
        // SQL surface trivial (one query per repository) is more important
        // than shaving microseconds with hand-tuned aggregates.
        var agents = await _agents.GetAllAgentsAsync();
        var pendingAgents = await _pendingAgents.GetPendingAgentsAsync();
        var policies = await _policies.GetAllPoliciesAsync();
        var jobs = await _jobs.GetAllBackupJobsAsync();
        var artifacts = await _artifacts.GetAllArtifactsAsync();

        var now = DateTime.UtcNow;

        return new DashboardSummaryDto(
            BuildAgentSummary(agents, now),
            pendingAgents.Count,
            BuildPolicySummary(policies),
            BuildJobSummary(jobs, policies, agents, now),
            BuildArtifactSummary(artifacts, jobs, policies));
    }

    private AgentSummaryDto BuildAgentSummary(IReadOnlyList<Agent> agents, DateTime now)
    {
        var online = 0;
        var stale = 0;
        var offline = 0;
        foreach (var agent in agents)
        {
            switch (_agentService.GetConnectivityStatus(agent, now))
            {
                case "online": online++; break;
                case "stale": stale++; break;
                default: offline++; break;
            }
        }

        return new AgentSummaryDto(online, stale, offline, agents.Count);
    }

    private static PolicySummaryDto BuildPolicySummary(IReadOnlyList<BackupPolicy> policies)
    {
        var active = 0;
        var fs = 0;
        var pg = 0;
        var my = 0;
        foreach (var policy in policies)
        {
            if (policy.IsEnabled) active++;
            switch (policy.Type)
            {
                case BackupPolicyType.FileSystem: fs++; break;
                case BackupPolicyType.PostgreSqlDump: pg++; break;
                case BackupPolicyType.MySqlDump: my++; break;
            }
        }

        return new PolicySummaryDto(active, policies.Count, new PolicyTypeBreakdownDto(fs, pg, my));
    }

    private static JobSummaryDto BuildJobSummary(
        IReadOnlyList<BackupJob> jobs,
        IReadOnlyList<BackupPolicy> policies,
        IReadOnlyList<Agent> agents,
        DateTime now)
    {
        var completed = 0;
        var running = 0;
        var failed = 0;
        foreach (var job in jobs)
        {
            switch (job.Status)
            {
                case BackupJobStatus.Completed: completed++; break;
                case BackupJobStatus.Running: running++; break;
                case BackupJobStatus.Failed: failed++; break;
            }
        }

        // Buckets are UTC days for the last 7 calendar days, oldest first.
        // The frontend converts to local labels with toLocaleDateString.
        var windowStart = now.Date.AddDays(-(Last7Days - 1));
        var dayCounts = new int[Last7Days];
        foreach (var job in jobs)
        {
            var day = job.StartedAt.Date;
            var offset = (day - windowStart).Days;
            if (offset >= 0 && offset < Last7Days)
            {
                dayCounts[offset]++;
            }
        }
        var last7Days = new List<JobsPerDayDto>(Last7Days);
        for (var i = 0; i < Last7Days; i++)
        {
            var day = windowStart.AddDays(i);
            last7Days.Add(new JobsPerDayDto(day.ToString("yyyy-MM-dd"), dayCounts[i]));
        }

        // Unresolved failures = the most recent job per policy is itself
        // a Failed run. A subsequent Completed run would clear the alert.
        // Jobs whose policy has been deleted with "keep history" can't
        // surface as actionable alerts — there's no policy to fix —
        // so we skip null PolicyId here.
        var policyNames = policies.ToDictionary(p => p.Id, p => p.Name);
        var unresolved = jobs
            .Where(j => j.PolicyId.HasValue)
            .GroupBy(j => j.PolicyId!.Value)
            .Select(g => g.OrderByDescending(j => j.StartedAt).First())
            .Where(j => j.Status == BackupJobStatus.Failed)
            .Select(j => new UnresolvedFailureDto(
                j.PolicyId!.Value,
                policyNames.TryGetValue(j.PolicyId!.Value, out var name) ? name : string.Empty,
                j.ErrorMessage))
            .ToList();

        // Recent: top 5 jobs by StartedAt desc, with server-resolved
        // policy + agent names so the frontend doesn't need to keep
        // /agents and /policies lists in memory just to render them.
        // For detached (orphan) jobs we fall back to the snapshot names.
        var agentNames = agents.ToDictionary(a => a.Id, a => a.Name);
        var recent = jobs
            .OrderByDescending(j => j.StartedAt)
            .Take(RecentLimit)
            .Select(j => new RecentJobDto(
                j.Id,
                ResolvePolicyDisplayName(j, policyNames),
                ResolveAgentDisplayName(j, agentNames),
                j.Status.ToString().ToLowerInvariant(),
                j.StartedAt))
            .ToList();

        return new JobSummaryDto(completed, running, failed, jobs.Count, last7Days, unresolved, recent);
    }

    private static ArtifactSummaryDto BuildArtifactSummary(
        IReadOnlyList<BackupArtifact> artifacts,
        IReadOnlyList<BackupJob> jobs,
        IReadOnlyList<BackupPolicy> policies)
    {
        var totalSize = artifacts.Sum(a => a.SizeBytes);
        var jobToPolicy = jobs.ToDictionary(j => j.Id, j => j.PolicyId);
        var policyNames = policies.ToDictionary(p => p.Id, p => p.Name);
        var policyTypes = policies.ToDictionary(p => p.Id, p => p.Type);

        // Filesystem vs database split via the originating policy type.
        // Artifacts whose policy is gone (deleted with "keep history")
        // count as filesystem — same fallback the artifact shelf uses.
        var database = 0;
        foreach (var artifact in artifacts)
        {
            if (jobToPolicy.TryGetValue(artifact.JobId, out var policyId)
                && policyId.HasValue
                && policyTypes.TryGetValue(policyId.Value, out var type)
                && type != BackupPolicyType.FileSystem)
            {
                database++;
            }
        }
        var filesystem = artifacts.Count - database;

        var recent = artifacts
            .OrderByDescending(a => a.CreatedAt)
            .Take(RecentLimit)
            .Select(a =>
            {
                var fallback = $"Artifact {ShortId(a.Id)}";
                string displayName;
                if (!string.IsNullOrWhiteSpace(a.FileName))
                {
                    displayName = a.FileName;
                }
                else if (jobToPolicy.TryGetValue(a.JobId, out var pid)
                    && pid.HasValue
                    && policyNames.TryGetValue(pid.Value, out var pn)
                    && !string.IsNullOrWhiteSpace(pn))
                {
                    displayName = pn;
                }
                else
                {
                    displayName = fallback;
                }
                return new RecentArtifactDto(a.Id, displayName, a.SizeBytes, a.CreatedAt);
            })
            .ToList();

        return new ArtifactSummaryDto(artifacts.Count, totalSize, filesystem, database, recent);
    }

    private static string ShortId(Guid id) => id.ToString("N")[..8];

    private static string ResolvePolicyDisplayName(BackupJob job, Dictionary<Guid, string> policyNames)
    {
        if (job.PolicyId.HasValue
            && policyNames.TryGetValue(job.PolicyId.Value, out var name)
            && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }
        if (!string.IsNullOrWhiteSpace(job.PolicyNameSnapshot))
        {
            return job.PolicyNameSnapshot;
        }
        return $"Backup job {ShortId(job.Id)}";
    }

    private static string ResolveAgentDisplayName(BackupJob job, Dictionary<Guid, string> agentNames)
    {
        if (job.AgentId.HasValue
            && agentNames.TryGetValue(job.AgentId.Value, out var name)
            && !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }
        if (!string.IsNullOrWhiteSpace(job.AgentNameSnapshot))
        {
            return job.AgentNameSnapshot;
        }
        return job.AgentId.HasValue ? $"Agent {ShortId(job.AgentId.Value)}" : "Agent (deleted)";
    }
}
