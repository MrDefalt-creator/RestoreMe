using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Policies;

public record UpdateBackupPolicyRequest(
    [Required] Guid AgentId,
    [Required] string Name,
    [Required] string SourcePath,
    [Required] int IntervalSeconds,
    [Required] bool IsEnabled
);
