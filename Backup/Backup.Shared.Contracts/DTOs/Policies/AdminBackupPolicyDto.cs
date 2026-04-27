using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Policies;

public record AdminBackupPolicyDto(
    [Required] Guid Id,
    [Required] Guid AgentId,
    [Required] string Type,
    [Required] string Name,
    [Required] string SourcePath,
    [Required] bool IsEnabled,
    [Required] int IntervalSeconds,
    [Required] DateTime CreatedAt,
    [Required] DateTime NextRunAt,
    DateTime? LastRunAt,
    BackupPolicyDatabaseSettingsDto? DatabaseSettings
);
