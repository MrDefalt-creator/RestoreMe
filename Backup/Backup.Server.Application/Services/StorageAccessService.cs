using Backup.Server.Application.Interfaces;
using Backup.Server.Infrastructure.Options;
using Backup.Shared.Contracts.DTOs.Jobs;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Backup.Server.Application.Services;

public class StorageAccessService : IStorageAccessService
{
    private readonly IMinioClient _minioClient;
    private readonly StorageOptions _storageOptions;

    public StorageAccessService(
        IMinioClient minioClient,
        IOptions<StorageOptions> storageOptions)
    {
        _minioClient = minioClient;
        _storageOptions = storageOptions.Value;
    }

    private async Task EnsureBucketExistsAsync(CancellationToken cancellationToken)
    {
        var exists = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(_storageOptions.BucketName),
            cancellationToken);

        if (!exists)
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(_storageOptions.BucketName),
                cancellationToken);
        }
    }

    public async Task<UploadTicketResponse> CreateUploadTicketAsync(
        Guid backupJobId,
        Guid policyId,
        Guid agentId,
        string fileName,
        string contentType,
        CancellationToken cancellationToken)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        string safeFileName = fileName.Replace("\\", "/");
        string objectKey = $"{agentId}/{policyId}/{backupJobId}/{safeFileName}";
        int expirySeconds = _storageOptions.UploadUrlExpirySeconds;
        var expiresAtUtc = DateTime.UtcNow.AddSeconds(expirySeconds);

        string uploadUrl = await _minioClient.PresignedPutObjectAsync(
            new PresignedPutObjectArgs()
                .WithBucket(_storageOptions.BucketName)
                .WithObject(objectKey)
                .WithExpiry(expirySeconds));

        return new UploadTicketResponse(
            _storageOptions.BucketName,
            objectKey,
            uploadUrl,
            expiresAtUtc);
    }
}