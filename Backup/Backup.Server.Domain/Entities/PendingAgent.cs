using Backup.Server.Domain.Enums;

namespace Backup.Server.Domain.Entities;

public class PendingAgent
{
    public Guid Id { get; set; }

    public string MachineName { get; set; } = null!;
    public string OsType { get; set; } = null!;
    public string Version { get; set; } = null!;
    
    public PendingAgentStatus Status { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // null = ещё не подтверждён
    public Guid? ApprovedAgentId { get; set; }
    
    public Agent? ApprovedAgent { get; set; }
}