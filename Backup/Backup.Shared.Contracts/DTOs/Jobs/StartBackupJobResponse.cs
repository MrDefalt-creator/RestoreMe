using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Jobs;

public record StartBackupJobResponse(
    [Required] Guid Id
    );