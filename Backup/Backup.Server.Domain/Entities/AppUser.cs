using Backup.Server.Domain.Enums;

namespace Backup.Server.Domain.Entities;

public class AppUser
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public string NormalizedUsername { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public AppUserRole Role { get; set; } = AppUserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Bumped on password change / role change so any JWT issued before the
    // bump fails OnTokenValidated. New users start with a fresh stamp.
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    // When true, only password-change / logout endpoints are reachable —
    // forces the bootstrap admin (and any user the admin password-reset)
    // to set a fresh secret before doing anything else.
    public bool MustChangePassword { get; set; }

    // Brute-force / credential-stuffing mitigation. AuthService bumps the
    // counter on every failed login; once it hits the policy threshold,
    // LockedUntilUtc is set to "now + 15 min" and the user can't sign in
    // even with the right password until that window expires. A
    // successful login resets both fields.
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockedUntilUtc { get; set; }

    // JSON-encoded array of recent password hashes (most-recent-first).
    // ChangePassword/SetPassword rejects a new password that verifies
    // against any of these so users can't ping-pong between two old
    // secrets. Cap is enforced at the service layer (currently 5).
    public string? PasswordHistory { get; set; }
}
