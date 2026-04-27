using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Policies;

public record BackupPolicyDatabaseSettingsDto(
    [Required] string Engine,
    [Required] string AuthMode,
    string? Host,
    int? Port,
    [Required] string DatabaseName,
    string? Username,
    string? Password
);
