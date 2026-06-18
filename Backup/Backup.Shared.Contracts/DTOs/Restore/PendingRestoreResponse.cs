using Backup.Shared.Contracts.DTOs.Policies;

namespace Backup.Shared.Contracts.DTOs.Restore;

public record PendingRestoreResponse(
    Guid JobId,
    string ObjectKey,
    string FileName,
    string PolicyType,
    string SourcePath,
    BackupPolicyDatabaseSettingsDto? DatabaseSettings);
