using Backup.Server.Domain.Enums;

namespace Backup.Server.Domain.Entities;

public class RestoreJob
{
    public Guid Id { get; set; }
    public RestoreJobStatus Status { get; set; } = RestoreJobStatus.Pending;
    public Guid ArtifactId { get; set; }
    public BackupArtifact Artifact { get; set; } = null!;
    public Guid AgentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
