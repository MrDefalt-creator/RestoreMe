using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Integrity;

public record UpdateIntegrityScrubSettingsRequest(
    bool IsEnabled,
    [Range(1, 3650)] int IntervalDays,
    [Range(0, 1439)] int RunAtMinutesUtc,
    [Range(1, 1000000)] int BatchSize
);
