namespace Backup.Shared.Contracts.DTOs.Artifacts;

public record ArtifactVerifyResultDto(
    Guid Id,
    string IntegrityStatus,
    DateTime? LastVerifiedAt
);
