namespace Backup.Server.Infrastructure.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string Endpoint { get; set; } = null!;
    public string? PublicEndpoint { get; set; }
    public string AccessKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
    public string BucketName { get; set; } = null!;
    public bool UseSsl { get; set; }

    // Static expiry, used when UseAdaptiveExpiry = false. Also acts as a
    // fallback if the agent does not report a payload size.
    public int UploadUrlExpirySeconds { get; set; } = 9000;

    // Optional separate expiry for download tickets (agent restore flow).
    // 0/null -> fall back to UploadUrlExpirySeconds.
    public int? DownloadUrlExpirySeconds { get; set; }

    // Adaptive expiry: expiry = AdaptiveBaseSeconds + sizeGB * AdaptivePerGbSeconds,
    // hard-capped at MinIO's 7-day presigned URL ceiling. Tuned for conservative
    // ~3.5 MB/s sustained throughput per gigabyte by default.
    public bool UseAdaptiveExpiry { get; set; } = true;
    public int AdaptiveBaseSeconds { get; set; } = 600;
    public int AdaptivePerGbSeconds { get; set; } = 300;
}
