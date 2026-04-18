namespace Backup.Server.Domain.Entities;

public class BackupPolicy
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;
    public string SourcePath { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public int IntervalSeconds { get; set; }
    
    public DateTime NextRunAt { get; set; }
    
    public DateTime? LastRunAt { get; set; }

    public Guid AgentId { get; set; }
    public Agent Agent { get; set; } = null!;

    public ICollection<BackupJob> Jobs { get; set; } = new List<BackupJob>();
}