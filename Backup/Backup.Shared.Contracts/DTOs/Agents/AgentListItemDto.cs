using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Agents;

public record AgentListItemDto(
    [Required] Guid Id,
    [Required] string Name,
    [Required] string MachineName,
    [Required] string OsType,
    [Required] string Version,
    [Required] string Status,
    [Required] DateTime CreatedAt,
    DateTime? LastSeenAt
);
