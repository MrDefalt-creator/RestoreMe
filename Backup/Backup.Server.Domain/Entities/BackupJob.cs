using Backup.Server.Domain.Enums;

namespace Backup.Server.Domain.Entities;

public class BackupJob
{
    public Guid Id { get; set; }

    public BackupJobStatus Status { get; set; } = BackupJobStatus.Pending;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }

    // Nullable so deleting the owning agent / policy can detach the job
    // (history-preserving selective delete) instead of cascade-removing it.
    public Guid? AgentId { get; set; }
    public Agent? Agent { get; set; }

    public Guid? PolicyId { get; set; }
    public BackupPolicy? Policy { get; set; }

    // Captured at delete time so orphaned rows still render the agent /
    // policy that produced them. Null while the original FK is still set.
    public string? AgentNameSnapshot { get; set; }
    public string? PolicyNameSnapshot { get; set; }

    public ICollection<BackupArtifact> Artifacts { get; set; } = new List<BackupArtifact>();
}