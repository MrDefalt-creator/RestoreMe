using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Policies;

public record CreateBackupPolicyRequest(
    [Required] string Type,
    [Required][StringLength(150)] string Name,
    [StringLength(500)] string? SourcePath,
    [Required] int Interval,
    BackupPolicyDatabaseSettingsDto? DatabaseSettings
    );
