namespace Backup.Shared.Contracts.DTOs.Restore;

public record CreateRestoreRequest(
    Guid ArtifactId,
    Guid? TargetAgentId,
    string? TargetName,
    bool DryRun,
    bool Force);
