using Backup.Server.Domain.Enums;

namespace Backup.Server.Domain.Entities;

public class Agent
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;
    public string MachineName { get; set; } = null!;
    public string OsType { get; set; } = null!;
    public string Version { get; set; } = null!;

    public AgentStatus Status { get; set; } = AgentStatus.Offline;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }

    public ICollection<BackupPolicy> Policies { get; set; } = new List<BackupPolicy>();
    
    public PendingAgent? PendingAgent { get; set; }

}