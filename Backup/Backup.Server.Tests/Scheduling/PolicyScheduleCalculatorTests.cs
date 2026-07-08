using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Tests.Scheduling;

public sealed class PolicyScheduleCalculatorTests
{
    private static BackupPolicy IntervalPolicy(int seconds, string? tz = null, int? winStart = null, int? winEnd = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = "p",
        SourcePath = "/data",
        ScheduleKind = ScheduleKind.Interval,
        IntervalSeconds = seconds,
        TimeZoneId = tz,
        WindowStartMinutes = winStart,
        WindowEndMinutes = winEnd,
    };

    private static BackupPolicy CronPolicy(string expr, string tz) => new()
    {
        Id = Guid.NewGuid(),
        Name = "p",
        SourcePath = "/data",
        ScheduleKind = ScheduleKind.Cron,
        CronExpression = expr,
        TimeZoneId = tz,
    };

    // --- interval, no window (current behavior preserved) ---

    [Fact]
    public void Interval_NoWindow_AddsIntervalSeconds()
    {
        var now = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var next = PolicyScheduleCalculator.ComputeNextRun(IntervalPolicy(3600), now);
        Assert.Equal(now.AddHours(1), next);
        Assert.Equal(DateTimeKind.Utc, next.Kind);
    }

    // --- interval + window ---

    [Fact]
    public void Interval_CandidateInsideWindow_IsUnchanged()
    {
        // Window 22:00–06:00 UTC; candidate lands 23:00 UTC.
        var now = new DateTime(2026, 7, 8, 22, 0, 0, DateTimeKind.Utc);
        var policy = IntervalPolicy(3600, "Etc/UTC", winStart: 22 * 60, winEnd: 6 * 60);
        Assert.Equal(now.AddHours(1), PolicyScheduleCalculator.ComputeNextRun(policy, now));
    }

    [Fact]
    public void Interval_CandidateOutsideWindow_DefersToWindowStart()
    {
        // Candidate 11:00 UTC, window 22:00–06:00 → deferred to 22:00 same day.
        var now = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var policy = IntervalPolicy(3600, "Etc/UTC", winStart: 22 * 60, winEnd: 6 * 60);
        Assert.Equal(new DateTime(2026, 7, 8, 22, 0, 0, DateTimeKind.Utc),
            PolicyScheduleCalculator.ComputeNextRun(policy, now));
    }

    [Fact]
    public void Interval_WindowSpansMidnight_EarlyMorningInside()
    {
        // Candidate 05:00 UTC is inside a 22:00–06:00 window.
        var now = new DateTime(2026, 7, 8, 4, 0, 0, DateTimeKind.Utc);
        var policy = IntervalPolicy(3600, "Etc/UTC", winStart: 22 * 60, winEnd: 6 * 60);
        Assert.Equal(now.AddHours(1), PolicyScheduleCalculator.ComputeNextRun(policy, now));
    }

    [Fact]
    public void Interval_WindowInNonUtcZone_DefersInThatZone()
    {
        // Moscow is UTC+3 (no DST). Window 22:00–06:00 MSK = 19:00–03:00 UTC.
        // Candidate 12:00 UTC (15:00 MSK) → deferred to 22:00 MSK = 19:00 UTC.
        var now = new DateTime(2026, 7, 8, 11, 0, 0, DateTimeKind.Utc);
        var policy = IntervalPolicy(3600, "Europe/Moscow", winStart: 22 * 60, winEnd: 6 * 60);
        Assert.Equal(new DateTime(2026, 7, 8, 19, 0, 0, DateTimeKind.Utc),
            PolicyScheduleCalculator.ComputeNextRun(policy, now));
    }

    // --- cron ---

    [Fact]
    public void Cron_DailyAtThreeMoscow_ReturnsUtcEquivalent()
    {
        // 03:00 MSK = 00:00 UTC.
        var now = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var next = PolicyScheduleCalculator.ComputeNextRun(CronPolicy("0 3 * * *", "Europe/Moscow"), now);
        Assert.Equal(new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Cron_DstSpringForward_SkipsToValidTime()
    {
        // Europe/Berlin 2027-03-28: 02:00–03:00 does not exist.
        // Cronos maps a 02:30 daily schedule on that day to the shifted time (03:00 local = 01:00 UTC).
        var now = new DateTime(2027, 3, 28, 0, 0, 0, DateTimeKind.Utc);
        var next = PolicyScheduleCalculator.ComputeNextRun(CronPolicy("30 2 * * *", "Europe/Berlin"), now);
        Assert.Equal(new DateTime(2027, 3, 28, 1, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void Cron_WeeklyExpression_LandsOnRequestedWeekday()
    {
        // 2026-07-08 is a Wednesday; next Monday 04:00 UTC is 2026-07-13.
        var now = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var next = PolicyScheduleCalculator.ComputeNextRun(CronPolicy("0 4 * * 1", "Etc/UTC"), now);
        Assert.Equal(new DateTime(2026, 7, 13, 4, 0, 0, DateTimeKind.Utc), next);
    }

    // --- catch-up semantics: a past NextRunAt is the *agent's* concern;
    //     the calculator always schedules strictly after nowUtc ---

    [Fact]
    public void Cron_ComputedFromNow_NotFromMissedSlot()
    {
        // Even called long after a missed 03:00 slot, result is the NEXT 03:00, not the past one.
        var now = new DateTime(2026, 7, 8, 12, 0, 0, DateTimeKind.Utc);
        var next = PolicyScheduleCalculator.ComputeNextRun(CronPolicy("0 3 * * *", "Etc/UTC"), now);
        Assert.Equal(new DateTime(2026, 7, 9, 3, 0, 0, DateTimeKind.Utc), next);
    }

    // --- first run (creation) ---

    [Fact]
    public void FirstRun_IntervalNoWindow_IsNow()
    {
        var now = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(now, PolicyScheduleCalculator.ComputeFirstRun(IntervalPolicy(3600), now));
    }

    [Fact]
    public void FirstRun_IntervalOutsideWindow_DefersToWindowStart()
    {
        var now = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var policy = IntervalPolicy(3600, "Etc/UTC", winStart: 22 * 60, winEnd: 6 * 60);
        Assert.Equal(new DateTime(2026, 7, 8, 22, 0, 0, DateTimeKind.Utc),
            PolicyScheduleCalculator.ComputeFirstRun(policy, now));
    }

    [Fact]
    public void FirstRun_Cron_IsNextOccurrenceNotImmediate()
    {
        var now = new DateTime(2026, 7, 8, 10, 0, 0, DateTimeKind.Utc);
        var next = PolicyScheduleCalculator.ComputeFirstRun(CronPolicy("0 3 * * *", "Etc/UTC"), now);
        Assert.Equal(new DateTime(2026, 7, 9, 3, 0, 0, DateTimeKind.Utc), next);
    }
}
