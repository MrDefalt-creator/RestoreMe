namespace Backup.Shared.Contracts.DTOs.Integrity;

public record IntegrityScrubSettingsDto(
    bool IsEnabled,
    int IntervalDays,
    int RunAtMinutesUtc,
    int BatchSize,
    DateTime? LastRunAt,
    DateTime NextRunAt
);
