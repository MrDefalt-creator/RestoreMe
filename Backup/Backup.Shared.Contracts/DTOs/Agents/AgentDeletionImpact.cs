namespace Backup.Shared.Contracts.DTOs.Agents;

/// <summary>
/// Summary the UI pre-fetches before opening the "Delete agent" dialog
/// so the operator can see exactly how much state is about to disappear.
/// </summary>
public record AgentDeletionImpact(
    int PolicyCount,
    int BackupJobCount,
    int ArtifactCount,
    long TotalStorageBytes,
    int RestoreJobCount,
    int PendingRestoreJobCount);
