using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs;

public record PendingAgentStatusResponse(
    [Required] int Status,
    [Required] Guid? ApprovedAgentId
    );