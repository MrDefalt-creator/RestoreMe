namespace Backup.Server.Domain.Options;

public class RetentionOptions
{
    public const string SectionName = "Retention";

    // How often the retention cleanup sweep runs. Defaults to 24h.
    public int CleanupIntervalHours { get; set; } = 24;
}
