using Backup.Shared.Contracts.DTOs.Jobs;

namespace Backup.Server.Application.Interfaces;

public interface IStorageAccessService
{
    Task EnsureBucketExistsAsync(CancellationToken cancellationToken);

    Task<UploadTicketResponse> CreateUploadTicketAsync(
        Guid backupJobId,
        Guid policyId,
        Guid agentId,
        string fileName,
        string contentType,
        long sizeBytes,
        string? publicServerBaseUrl,
        CancellationToken cancellationToken);

    Task WriteObjectToAsync(
        string objectKey,
        Stream destination,
        CancellationToken cancellationToken);

    Task<StorageObjectInfo> GetObjectInfoAsync(
        string objectKey,
        CancellationToken cancellationToken);

    // Streams the object from storage and returns its SHA256 as a lowercase
    // hex string (same format the agent's ChecksumService produces).
    Task<string> ComputeObjectSha256Async(
        string objectKey,
        CancellationToken cancellationToken);

    Task<string> CreateDownloadTicketAsync(
        string objectKey,
        long sizeBytes,
        string? publicServerBaseUrl,
        CancellationToken cancellationToken);

    Task DeleteObjectAsync(string objectKey, CancellationToken cancellationToken);
}

public sealed record StorageObjectInfo(long SizeBytes);
