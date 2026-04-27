using Backup.Server.Domain.Enums;

namespace Backup.Server.Domain.Entities;

public class BackupPolicyDatabaseSettings
{
    public Guid PolicyId { get; set; }
    public BackupPolicy Policy { get; set; } = null!;

    public DatabaseEngine Engine { get; set; }
    public DatabaseDumpAuthMode AuthMode { get; set; }

    public string? Host { get; set; }
    public int? Port { get; set; }
    public string DatabaseName { get; set; } = null!;
    public string? Username { get; set; }
    public string? Password { get; set; }
}
