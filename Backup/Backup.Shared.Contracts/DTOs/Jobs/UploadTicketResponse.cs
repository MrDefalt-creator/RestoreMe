namespace Backup.Shared.Contracts.DTOs.Jobs;

public record UploadTicketResponse(
    string BucketName,
    string ObjectKey,
    string UploadUrl,
    DateTime ExpiresAtUtc
    );