using Backup.Server.Domain.Enums;

namespace Backup.Server.Domain.Entities;

public class BackupJob
{
    public Guid Id { get; set; }

    public BackupJobStatus Status { get; set; } = BackupJobStatus.Pending;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? ErrorMessage { get; set; }
    
    public Guid AgentId { get; set; }
    public Agent Agent { get; set; } = null!;
    
    public Guid PolicyId { get; set; }
    public BackupPolicy Policy { get; set; } = null!;

    public ICollection<BackupArtifact> Artifacts { get; set; } = new List<BackupArtifact>();
    
}