using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Agents;

public record PendingAgentStatusResponse(
    [Required] int Status,
    [Required] Guid? ApprovedAgentId
    );