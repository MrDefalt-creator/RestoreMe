namespace Backup.Server.Infrastructure.Options;

public class StorageOptions
{
    public const string SectionName = "Storage";

    public string Endpoint { get; set; } = null!;
    public string AccessKey { get; set; } = null!;
    public string SecretKey { get; set; } = null!;
    public string BucketName { get; set; } = null!;
    public bool UseSsl { get; set; }
    public int UploadUrlExpirySeconds { get; set; } = 9000;
}