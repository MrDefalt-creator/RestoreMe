using Backup.Server.Infrastructure.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;

namespace Backup.Server.Api.HealthChecks;

public sealed class MinioHealthCheck : IHealthCheck
{
    private readonly IMinioClient _minioClient;
    private readonly StorageOptions _storageOptions;

    public MinioHealthCheck(IMinioClient minioClient, IOptions<StorageOptions> storageOptions)
    {
        _minioClient = minioClient;
        _storageOptions = storageOptions.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _minioClient.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_storageOptions.BucketName),
                cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MinIO unreachable", ex);
        }
    }
}
