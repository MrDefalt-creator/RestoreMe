using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs;

public record AddArtifactBackupJobRequest(
    [Required] Guid JobId,
    [Required] string FileName,
    [Required] string ObjectKey,
    [Required] long Size,
    [Required] string Сhecksum
    );