using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Policies;

public record BackupPolicyDto(
    [Required] Guid Id,
    [Required] string Type,
    [Required] string Name,
    [Required] string SourcePath,
    [Required] bool IsEnabled,
    [Required] DateTime NexRunAt,
    BackupPolicyDatabaseSettingsDto? DatabaseSettings
    );
