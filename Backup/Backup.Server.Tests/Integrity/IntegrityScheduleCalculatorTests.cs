using Backup.Server.Application.Services;

namespace Backup.Server.Tests.Integrity;

public sealed class IntegrityScheduleCalculatorTests
{
    [Fact]
    public void NextRun_LaterToday_WhenTimeNotYetPassed()
    {
        var from = new DateTime(2026, 6, 19, 1, 0, 0, DateTimeKind.Utc); // 01:00
        var next = IntegrityScheduleCalculator.ComputeNextRun(from, intervalDays: 7, runAtMinutesUtc: 180); // 03:00
        Assert.Equal(new DateTime(2026, 6, 19, 3, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void NextRun_AdvancesByInterval_WhenTimeAlreadyPassed()
    {
        var from = new DateTime(2026, 6, 19, 5, 0, 0, DateTimeKind.Utc); // 05:00, past 03:00
        var next = IntegrityScheduleCalculator.ComputeNextRun(from, intervalDays: 7, runAtMinutesUtc: 180);
        Assert.Equal(new DateTime(2026, 6, 26, 3, 0, 0, DateTimeKind.Utc), next);
    }

    [Fact]
    public void NextRun_TreatsNonPositiveIntervalAsDaily()
    {
        var from = new DateTime(2026, 6, 19, 5, 0, 0, DateTimeKind.Utc);
        var next = IntegrityScheduleCalculator.ComputeNextRun(from, intervalDays: 0, runAtMinutesUtc: 180);
        Assert.Equal(new DateTime(2026, 6, 20, 3, 0, 0, DateTimeKind.Utc), next);
    }
}
