namespace Backup.Shared.Contracts.DTOs.Jobs;

public record AddArtifactRequest(
    Guid BackupJobId,
    string FileName,
    string ObjectKey,
    long Size,
    string Checksum
    );