using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Jobs;

public record AdminBackupJobDto(
    [Required] Guid Id,
    [Required] Guid AgentId,
    [Required] Guid PolicyId,
    [Required] string Status,
    [Required] DateTime StartedAt,
    DateTime? CompletedAt,
    string? ErrorMessage
);
