namespace Backup.Server.Domain.Entities;

// Single-row, admin-managed schedule for the background integrity scrub.
public class IntegrityScrubSettings
{
    public Guid Id { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int IntervalDays { get; set; } = 7;        // "когда" — every N days
    public int RunAtMinutesUtc { get; set; } = 180;   // "во сколько" — 03:00 UTC
    public int BatchSize { get; set; } = 50;          // artifacts re-hashed per run
    public DateTime? LastRunAt { get; set; }
    public DateTime NextRunAt { get; set; }
}
