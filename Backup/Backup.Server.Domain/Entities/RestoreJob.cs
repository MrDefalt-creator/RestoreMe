using Backup.Server.Domain.Enums;

namespace Backup.Server.Domain.Entities;

public class RestoreJob
{
    public Guid Id { get; set; }
    public RestoreJobStatus Status { get; set; } = RestoreJobStatus.Pending;

    // Artifact + originating-agent FKs go nullable so deleting an agent
    // (and optionally the artifact rows it produced) doesn't have to take
    // the restore history with it. Snapshots capture display strings
    // before detaching.
    public Guid? ArtifactId { get; set; }
    public BackupArtifact? Artifact { get; set; }
    public Guid? AgentId { get; set; }

    public string? AgentNameSnapshot { get; set; }
    public string? ArtifactFileNameSnapshot { get; set; }
    public string? ArtifactObjectKeySnapshot { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? TargetAgentId { get; set; }
    public string? TargetName { get; set; }
    public bool DryRun { get; set; }
    public bool Force { get; set; }
    public int? Progress { get; set; }
    public long? BytesTotal { get; set; }
    public long? BytesDone { get; set; }
    public string? LogTail { get; set; }
    public int? EtaSeconds { get; set; }
}
