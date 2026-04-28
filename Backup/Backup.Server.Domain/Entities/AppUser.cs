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
}
