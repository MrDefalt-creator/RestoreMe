namespace Backup.Shared.Contracts.DTOs.Restore;

public record FailRestoreJobRequest(Guid JobId, string ErrorMessage);
