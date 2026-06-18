namespace Backup.Shared.Contracts.DTOs.Dashboard;

public sealed record DashboardMetricsDto(
    string Period,
    DateTime From,
    DateTime To,
    IReadOnlyList<SuccessRatePointDto> SuccessRateTimeseries,
    IReadOnlyList<StorageGrowthPointDto> StorageGrowthTimeseries,
    IReadOnlyList<TopFailingPolicyDto> TopFailingPolicies,
    IReadOnlyList<EngineBreakdownDto> EngineBreakdown);

public sealed record SuccessRatePointDto(string Date, int Completed, int Failed);

public sealed record StorageGrowthPointDto(string Date, long CumulativeBytes);

public sealed record TopFailingPolicyDto(Guid PolicyId, string PolicyName, int FailureCount);

public sealed record EngineBreakdownDto(string Engine, int PolicyCount);
