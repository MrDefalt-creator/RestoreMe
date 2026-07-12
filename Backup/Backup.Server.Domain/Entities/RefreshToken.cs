namespace Backup.Server.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string TokenHash { get; set; } = null!;        // SHA-256 hex of the raw token
    public Guid FamilyId { get; set; }                     // rotation lineage
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }             // absolute cap
    public DateTime? LastUsedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? UserAgent { get; set; }
    public string? CreatedByIp { get; set; }
    // Whether this session's refresh cookie should persist across browser
    // restarts (remember-me). Carried across rotation so a refresh never
    // upgrades a session-only cookie into a persistent one.
    public bool Persistent { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
}
