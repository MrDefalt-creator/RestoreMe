namespace Backup.Server.Domain.Options;

public class IntegrityOptions
{
    public const string SectionName = "Integrity";

    // How often the worker wakes to test whether a scheduled scrub is due.
    // This is NOT the scrub cadence (that lives in IntegrityScrubSettings).
    public int CheckIntervalSeconds { get; set; } = 60;
}
