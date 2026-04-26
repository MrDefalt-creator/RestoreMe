using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Jobs;

public record FailedBackupJobRequest(
    [Required] Guid JobId,
    [Required] string ErrorMessage
    );