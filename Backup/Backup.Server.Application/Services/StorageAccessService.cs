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
        string? publicServerBaseUrl,
        CancellationToken cancellationToken)
    {
        await EnsureBucketExistsAsync(cancellationToken);

        string safeFileName = fileName.Replace("\\", "/");
        string objectKey = $"{agentId}/{policyId}/{backupJobId}/{safeFileName}";
        int expirySeconds = _storageOptions.UploadUrlExpirySeconds;
        var expiresAtUtc = DateTime.UtcNow.AddSeconds(expirySeconds);
        var publicEndpoint = ResolvePublicEndpoint(
            _storageOptions.PublicEndpoint,
            publicServerBaseUrl,
            _storageOptions.Endpoint,
            _storageOptions.UseSsl);
        var signingClient = CreateSigningClient(
            publicEndpoint ?? _storageOptions.Endpoint,
            publicEndpoint is not null
                ? IsSslEnabled(publicEndpoint, _storageOptions.UseSsl)
                : _storageOptions.UseSsl);

        string uploadUrl = await signingClient.PresignedPutObjectAsync(
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

    public async Task<Stream> OpenDownloadStreamAsync(
        string objectKey,
        CancellationToken cancellationToken)
    {
        var stream = new MemoryStream();

        await _minioClient.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(_storageOptions.BucketName)
                .WithObject(objectKey)
                .WithCallbackStream(sourceStream =>
                {
                    sourceStream.CopyTo(stream);
                }),
            cancellationToken);

        stream.Position = 0;
        return stream;
    }

    private static string? ResolvePublicEndpoint(
        string? configuredPublicEndpoint,
        string? publicServerBaseUrl,
        string storageEndpoint,
        bool useSsl)
    {
        if (!string.IsNullOrWhiteSpace(configuredPublicEndpoint))
        {
            return configuredPublicEndpoint;
        }

        if (string.IsNullOrWhiteSpace(publicServerBaseUrl))
        {
            return null;
        }

        var normalizedServerBaseUrl = publicServerBaseUrl.Contains("://", StringComparison.Ordinal)
            ? publicServerBaseUrl
            : $"{(useSsl ? "https" : "http")}://{publicServerBaseUrl}";

        if (!Uri.TryCreate(normalizedServerBaseUrl, UriKind.Absolute, out var serverUri))
        {
            return null;
        }

        var normalizedStorageEndpoint = storageEndpoint.Contains("://", StringComparison.Ordinal)
            ? storageEndpoint
            : $"{(useSsl ? "https" : "http")}://{storageEndpoint}";

        if (!Uri.TryCreate(normalizedStorageEndpoint, UriKind.Absolute, out var storageUri))
        {
            return null;
        }

        var builder = new UriBuilder(serverUri)
        {
            Port = storageUri.IsDefaultPort ? -1 : storageUri.Port
        };

        return builder.Uri.ToString();
    }

    private IMinioClient CreateSigningClient(string endpoint, bool useSsl)
    {
        var parsedEndpoint = ParseEndpoint(endpoint, useSsl);

        return new MinioClient()
            .WithEndpoint(parsedEndpoint.Host, parsedEndpoint.Port)
            .WithCredentials(_storageOptions.AccessKey, _storageOptions.SecretKey)
            .WithSSL(parsedEndpoint.UseSsl)
            .Build();
    }

    private static (string Host, int Port, bool UseSsl) ParseEndpoint(string endpoint, bool defaultUseSsl)
    {
        if (!string.IsNullOrWhiteSpace(endpoint) &&
            Uri.TryCreate(
                endpoint.Contains("://", StringComparison.Ordinal)
                    ? endpoint
                    : $"{(defaultUseSsl ? "https" : "http")}://{endpoint}",
                UriKind.Absolute,
                out var endpointUri))
        {
            return (
                endpointUri.Host,
                endpointUri.IsDefaultPort
                    ? (string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80)
                    : endpointUri.Port,
                string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
        }

        var parts = endpoint.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && int.TryParse(parts[1], out var port))
        {
            return (parts[0], port, defaultUseSsl);
        }

        return (endpoint, defaultUseSsl ? 443 : 80, defaultUseSsl);
    }

    private static bool IsSslEnabled(string endpoint, bool fallbackUseSsl)
    {
        if (!endpoint.Contains("://", StringComparison.Ordinal))
        {
            return fallbackUseSsl;
        }

        return Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) &&
               string.Equals(endpointUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }
}
