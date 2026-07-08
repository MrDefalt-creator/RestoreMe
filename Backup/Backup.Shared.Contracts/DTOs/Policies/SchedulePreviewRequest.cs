namespace Backup.Shared.Contracts.DTOs.Policies;

public record SchedulePreviewRequest(
    string? ScheduleKind,
    int? IntervalSeconds,
    string? CronExpression,
    string? TimeZoneId,
    int? WindowStartMinutes,
    int? WindowEndMinutes);
