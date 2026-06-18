namespace Backup.Server.Application.Services;

/// <summary>
/// Pure next-run calculation: the next occurrence of the configured time-of-day
/// (minutes since midnight UTC) at least one tick in the future, advancing by
/// IntervalDays when today's slot has already passed.
/// </summary>
public static class IntegrityScheduleCalculator
{
    public static DateTime ComputeNextRun(DateTime fromUtc, int intervalDays, int runAtMinutesUtc)
    {
        var step = intervalDays > 0 ? intervalDays : 1;
        var minutes = Math.Clamp(runAtMinutesUtc, 0, 24 * 60 - 1);
        var candidate = fromUtc.Date.AddMinutes(minutes);
        while (candidate <= fromUtc)
        {
            candidate = candidate.AddDays(step);
        }

        return DateTime.SpecifyKind(candidate, DateTimeKind.Utc);
    }
}
