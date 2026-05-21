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

    // Tracks the health state the sweep service most recently fired a
    // notification for. NULL means "never observed yet" — the first
    // sweep records the current state without firing, so the operator
    // doesn't get a startup-time "agent offline" for every agent that
    // happens to be down at boot.
    public bool? LastNotifiedOnline { get; set; }

    public ICollection<BackupPolicy> Policies { get; set; } = new List<BackupPolicy>();

    public PendingAgent? PendingAgent { get; set; }

}
