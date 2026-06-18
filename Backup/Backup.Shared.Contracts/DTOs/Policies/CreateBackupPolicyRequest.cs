using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Policies;

public record CreateBackupPolicyRequest(
    [Required] string Type,
    [Required][StringLength(150)] string Name,
    [StringLength(500)] string? SourcePath,
    [Required] int Interval,
    BackupPolicyDatabaseSettingsDto? DatabaseSettings,
    [Range(1, 3650)] int? RetentionDays = null,
    [Range(1, 10000)] int? RetentionMaxCount = null,
    [Range(1, long.MaxValue)] long? RetentionMaxTotalBytes = null
    );
