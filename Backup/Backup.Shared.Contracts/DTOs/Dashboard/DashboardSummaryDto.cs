namespace Backup.Shared.Contracts.DTOs.Dashboard;

public sealed record DashboardSummaryDto(
    AgentSummaryDto Agents,
    int PendingAgentsCount,
    PolicySummaryDto Policies,
    JobSummaryDto Jobs,
    ArtifactSummaryDto Artifacts);

public sealed record AgentSummaryDto(int Online, int Stale, int Offline, int Total);

public sealed record PolicySummaryDto(
    int Active,
    int Total,
    PolicyTypeBreakdownDto ByType);

public sealed record PolicyTypeBreakdownDto(int Filesystem, int Postgres, int Mysql);

public sealed record JobSummaryDto(
    int Completed,
    int Running,
    int Failed,
    int Total,
    IReadOnlyList<JobsPerDayDto> Last7Days,
    IReadOnlyList<UnresolvedFailureDto> UnresolvedFailures,
    IReadOnlyList<RecentJobDto> Recent);

public sealed record JobsPerDayDto(string Date, int Count);

public sealed record UnresolvedFailureDto(Guid PolicyId, string PolicyName, string? ErrorMessage);

public sealed record RecentJobDto(
    Guid Id,
    string Title,
    string AgentName,
    string Status,
    DateTime StartedAt);

public sealed record ArtifactSummaryDto(
    int Total,
    long TotalSize,
    // Split by originating policy type (artifacts of deleted policies
    // count as filesystem, matching the frontend's display fallback).
    int Filesystem,
    int Database,
    IReadOnlyList<RecentArtifactDto> Recent);

public sealed record RecentArtifactDto(
    Guid Id,
    string DisplayName,
    long Size,
    DateTime CreatedAt);
