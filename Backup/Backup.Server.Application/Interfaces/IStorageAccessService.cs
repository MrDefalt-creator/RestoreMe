using Backup.Shared.Contracts.DTOs.Jobs;

namespace Backup.Server.Application.Interfaces;

public interface IStorageAccessService
{
    Task<UploadTicketResponse> CreateUploadTicketAsync(
        Guid backupJobId,
        Guid policyId,
        Guid agentId,
        string fileName,
        string contentType,
        string? publicServerBaseUrl,
        CancellationToken cancellationToken);

    Task<Stream> OpenDownloadStreamAsync(
        string objectKey,
        CancellationToken cancellationToken);

    Task<StorageObjectInfo> GetObjectInfoAsync(
        string objectKey,
        CancellationToken cancellationToken);

    Task<string> CreateDownloadTicketAsync(
        string objectKey,
        CancellationToken cancellationToken);

    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed record StorageObjectInfo(long SizeBytes);
