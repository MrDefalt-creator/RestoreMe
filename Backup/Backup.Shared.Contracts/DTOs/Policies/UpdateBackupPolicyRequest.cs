using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Policies;

public record UpdateBackupPolicyRequest(
    [Required] Guid AgentId,
    [Required] string Type,
    [Required] string Name,
    string? SourcePath,
    [Required] int IntervalSeconds,
    [Required] bool IsEnabled,
    BackupPolicyDatabaseSettingsDto? DatabaseSettings,
    [Range(1, 3650)] int? RetentionDays = null,
    [Range(1, 10000)] int? RetentionMaxCount = null,
    [Range(1, long.MaxValue)] long? RetentionMaxTotalBytes = null
);
