using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs;

public record BackupPolicyDto(
    [Required] Guid Id,
    [Required] string Name,
    [Required] string SourcePath,
    [Required] bool IsEnabled
    );