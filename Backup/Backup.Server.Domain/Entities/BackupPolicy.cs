using Backup.Server.Domain.Enums;

namespace Backup.Server.Domain.Entities;

public class BackupPolicy
{
    public Guid Id { get; set; }

    public BackupPolicyType Type { get; set; } = BackupPolicyType.FileSystem;
    public string Name { get; set; } = null!;
    public string SourcePath { get; set; } = null!;
    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public int IntervalSeconds { get; set; }

    public ScheduleKind ScheduleKind { get; set; } = ScheduleKind.Interval;

    // Standard 5-field cron; required when ScheduleKind == Cron.
    public string? CronExpression { get; set; }

    // IANA timezone id; required for Cron and when a window is set.
    public string? TimeZoneId { get; set; }

    // Interval-only backup window, minutes-of-day in TimeZoneId.
    // Both-or-neither; Start > End means the window spans midnight.
    public int? WindowStartMinutes { get; set; }
    public int? WindowEndMinutes { get; set; }

    public DateTime NextRunAt { get; set; }
    
    public DateTime? LastRunAt { get; set; }

    public Guid AgentId { get; set; }
    public Agent Agent { get; set; } = null!;
    public BackupPolicyDatabaseSettings? DatabaseSettings { get; set; }

    public int? RetentionDays { get; set; }

    // Keep at most the newest N artifacts of this policy. null = unlimited.
    public int? RetentionMaxCount { get; set; }

    // Keep at most this many bytes of artifacts for this policy; oldest beyond
    // the budget are pruned (newest is always preserved). null = unlimited.
    public long? RetentionMaxTotalBytes { get; set; }

    public int ConsecutiveFailureCount { get; set; }
    public string? LastFailureReason { get; set; }
    public DateTime? AutoDisabledAt { get; set; }

    public ICollection<BackupJob> Jobs { get; set; } = new List<BackupJob>();
}
