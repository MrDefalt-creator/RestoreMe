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
    public DateTime? ApprovedAt { get; set; }

    // Incremented when an admin revokes the agent. Embedded in the issued
    // JWT as "tokver"; OnTokenValidated rejects any token whose version
    // is older than the live value, so a compromised agent can be locked
    // out without rotating the global JWT signing key.
    public int TokenVersion { get; set; } = 1;

    public ICollection<BackupPolicy> Policies { get; set; } = new List<BackupPolicy>();

    public PendingAgent? PendingAgent { get; set; }

}
