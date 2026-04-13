using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs;

public record StartBackupJobResponse(
    [Required] Guid Id
    );