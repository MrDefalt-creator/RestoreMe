using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Application.Services;

/// <summary>
/// Pure next-run computation for backup policies. Interval policies add
/// IntervalSeconds and defer into the optional backup window; cron policies
/// delegate to Cronos with the policy's IANA timezone (DST-correct).
/// All inputs/outputs are UTC. Assumes the schedule has already passed
/// PolicyScheduleValidator — invalid cron/timezone throws here.
/// </summary>
public static class PolicyScheduleCalculator
{
    public static DateTime ComputeNextRun(BackupPolicy policy, DateTime nowUtc)
    {
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        if (policy.ScheduleKind == ScheduleKind.Cron)
        {
            var expression = Cronos.CronExpression.Parse(policy.CronExpression);
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId!);
            return expression.GetNextOccurrence(nowUtc, timeZone)
                ?? throw new InvalidOperationException(
                    $"Cron expression '{policy.CronExpression}' has no future occurrence.");
        }

        return ApplyWindow(policy, nowUtc.AddSeconds(policy.IntervalSeconds));
    }

    public static DateTime ComputeFirstRun(BackupPolicy policy, DateTime nowUtc)
    {
        nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        return policy.ScheduleKind == ScheduleKind.Cron
            ? ComputeNextRun(policy, nowUtc)
            : ApplyWindow(policy, nowUtc);
    }

    private static DateTime ApplyWindow(BackupPolicy policy, DateTime candidateUtc)
    {
        if (policy.WindowStartMinutes is not int start || policy.WindowEndMinutes is not int end)
        {
            return candidateUtc;
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(policy.TimeZoneId!);
        var local = TimeZoneInfo.ConvertTimeFromUtc(candidateUtc, timeZone);
        var minuteOfDay = local.Hour * 60 + local.Minute;

        var inside = start < end
            ? minuteOfDay >= start && minuteOfDay < end
            : minuteOfDay >= start || minuteOfDay < end; // window spans midnight

        if (inside)
        {
            return candidateUtc;
        }

        var windowStart = local.Date.AddMinutes(start);
        if (windowStart <= local)
        {
            windowStart = windowStart.AddDays(1);
        }

        // A DST spring-forward can make the window start a nonexistent local time.
        if (timeZone.IsInvalidTime(windowStart))
        {
            // Assumes a 1-hour DST shift (true for the overwhelming majority of IANA zones).
            windowStart = windowStart.AddHours(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(windowStart, DateTimeKind.Unspecified), timeZone);
    }
}
