using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Jobs;

public record StartBackupJobRequest(
    [Required] Guid AgentId,
    [Required] Guid PolicyId
    );