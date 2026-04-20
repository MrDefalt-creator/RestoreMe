namespace Backup.Shared.Contracts.DTOs;

public record ConfirmUploadRequest(
    Guid BackupJobId,
    string FileName,
    string ObjectKey,
    long Size,
    string Checksum
    );