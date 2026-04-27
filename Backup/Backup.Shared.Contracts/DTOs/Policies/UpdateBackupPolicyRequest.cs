using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Policies;

public record UpdateBackupPolicyRequest(
    [Required] Guid AgentId,
    [Required] string Type,
    [Required] string Name,
    string? SourcePath,
    [Required] int IntervalSeconds,
    [Required] bool IsEnabled,
    BackupPolicyDatabaseSettingsDto? DatabaseSettings
);
