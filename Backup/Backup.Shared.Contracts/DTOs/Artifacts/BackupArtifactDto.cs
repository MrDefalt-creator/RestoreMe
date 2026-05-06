using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Artifacts;

public record BackupArtifactDto(
    [Required] Guid Id,
    [Required] Guid JobId,
    [Required] string FileName,
    [Required] string ObjectKey,
    [Required] long Size,
    [Required] string Checksum,
    [Required] DateTime CreatedAt
);
