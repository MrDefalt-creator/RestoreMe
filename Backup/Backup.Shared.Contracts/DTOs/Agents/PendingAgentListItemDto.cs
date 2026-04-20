using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Agents;

public record PendingAgentListItemDto(
    [Required] Guid Id,
    [Required] string MachineName,
    [Required] string OsType,
    [Required] string Version,
    [Required] string Status,
    [Required] DateTime CreatedAt,
    Guid? ApprovedAgentId
);
