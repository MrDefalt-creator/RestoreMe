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
}
