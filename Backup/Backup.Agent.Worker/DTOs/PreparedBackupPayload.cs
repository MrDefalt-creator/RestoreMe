namespace Backup.Agent.Worker.DTOs;

public sealed record PreparedBackupPayload(
    string FilePath,
    string FileName,
    string ContentType,
    bool ShouldDeleteAfterUpload
);
