namespace Backup.Shared.Contracts.DTOs.Restore;

public record RestoreStatusResponse(
    Guid Id,
    string Status,
    int? Progress,
    long? BytesTotal,
    long? BytesDone,
    string? LogTail,
    int? EtaSeconds);
