namespace Backup.Server.Domain.Entities;

public class BackupArtifact
{
    public Guid Id { get; set; }

    public string ObjectKey { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public long SizeBytes { get; set; }
    public string Checksum { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid JobId { get; set; }
    public BackupJob Job { get; set; } = null!;
}