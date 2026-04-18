namespace Backup.Shared.Contracts.DTOs.Jobs;

public record RequestUploadTicketRequest(
    Guid BackupJobId,
    Guid PolicyId,
    string FileName,
    string ContentType
    );