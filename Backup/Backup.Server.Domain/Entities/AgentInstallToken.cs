namespace Backup.Server.Domain.Entities;

// Single-use, short-lived token an admin generates to install one specific
// agent. The plaintext token leaves the server exactly once (in the
// generation response) and only its SHA-256 hash is stored. When the
// agent presents the token during enrollment we look up by hash, atomically
// mark it consumed, and auto-approve the agent.
//
// Replaces the shared AgentEnrollment:EnrollmentToken model where one
// leak compromised every agent slot.
public class AgentInstallToken
{
    public Guid Id { get; set; }

    // SHA-256 of the plaintext token. 32 bytes. The plaintext is never
    // persisted — it's returned once at creation and discarded.
    public byte[] TokenHash { get; set; } = Array.Empty<byte>();

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    // Admin (or operator) who minted the token. Used for audit trail.
    public Guid CreatedByUserId { get; set; }

    // Set atomically when the token is consumed by an enrolling agent.
    // Null while the token is still usable.
    public DateTime? UsedAt { get; set; }
    public string? UsedByMachineName { get; set; }

    // Optional human-readable name picked by the admin in the wizard. If
    // set, the resulting Agent.Name comes from here; otherwise we fall
    // back to the request MachineName.
    public string? PreApprovedName { get; set; }
}
