using Backup.Server.Application.Services;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Tests.Scheduling;

public sealed class PolicyScheduleValidatorTests
{
    private static PolicyScheduleInput Interval(int? seconds = 900, string? tz = null, int? ws = null, int? we = null)
        => new("interval", seconds, null, tz, ws, we);

    private static PolicyScheduleInput Cron(string? expr = "0 3 * * *", string? tz = "Europe/Moscow",
        int? ws = null, int? we = null)
        => new("cron", null, expr, tz, ws, we);

    [Fact]
    public void NullKind_DefaultsToInterval()
    {
        var result = PolicyScheduleValidator.Validate(new PolicyScheduleInput(null, 900, null, null, null, null));
        Assert.Equal(ScheduleKind.Interval, result.Kind);
        Assert.Equal(900, result.IntervalSeconds);
    }

    [Fact]
    public void UnknownKind_Throws()
        => Assert.Throws<InvalidOperationException>(
            () => PolicyScheduleValidator.Validate(new PolicyScheduleInput("hourly", 900, null, null, null, null)));

    [Fact]
    public void Interval_MissingOrNonPositiveSeconds_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PolicyScheduleValidator.Validate(Interval(seconds: null)));
        Assert.Throws<InvalidOperationException>(() => PolicyScheduleValidator.Validate(Interval(seconds: 0)));
    }

    [Fact]
    public void Interval_ValidWindow_Normalizes()
    {
        var result = PolicyScheduleValidator.Validate(Interval(900, "Europe/Moscow", 22 * 60, 6 * 60));
        Assert.Equal(ScheduleKind.Interval, result.Kind);
        Assert.Equal(22 * 60, result.WindowStartMinutes);
        Assert.Null(result.CronExpression);
    }

    [Fact]
    public void Interval_HalfSetWindow_Throws()
        => Assert.Throws<InvalidOperationException>(
            () => PolicyScheduleValidator.Validate(Interval(900, "Europe/Moscow", ws: 22 * 60, we: null)));

    [Fact]
    public void Interval_WindowWithoutTimezone_Throws()
        => Assert.Throws<InvalidOperationException>(
            () => PolicyScheduleValidator.Validate(Interval(900, tz: null, ws: 22 * 60, we: 6 * 60)));

    [Fact]
    public void Interval_WindowOutOfRangeOrDegenerate_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => PolicyScheduleValidator.Validate(Interval(900, "Etc/UTC", ws: -1, we: 360)));
        Assert.Throws<InvalidOperationException>(
            () => PolicyScheduleValidator.Validate(Interval(900, "Etc/UTC", ws: 1440, we: 360)));
        Assert.Throws<InvalidOperationException>(
            () => PolicyScheduleValidator.Validate(Interval(900, "Etc/UTC", ws: 360, we: 360)));
    }

    [Fact]
    public void Cron_Valid_NormalizesIntervalToZero()
    {
        var result = PolicyScheduleValidator.Validate(Cron());
        Assert.Equal(ScheduleKind.Cron, result.Kind);
        Assert.Equal(0, result.IntervalSeconds);
        Assert.Equal("0 3 * * *", result.CronExpression);
        Assert.Equal("Europe/Moscow", result.TimeZoneId);
    }

    [Fact]
    public void Cron_MissingOrInvalidExpression_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PolicyScheduleValidator.Validate(Cron(expr: null)));
        Assert.Throws<InvalidOperationException>(() => PolicyScheduleValidator.Validate(Cron(expr: "not a cron")));
        Assert.Throws<InvalidOperationException>(() => PolicyScheduleValidator.Validate(Cron(expr: "0 25 * * *")));
    }

    [Fact]
    public void Cron_MissingOrUnknownTimezone_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PolicyScheduleValidator.Validate(Cron(tz: null)));
        Assert.Throws<InvalidOperationException>(() => PolicyScheduleValidator.Validate(Cron(tz: "Mars/Olympus")));
    }

    [Fact]
    public void Cron_WithWindow_Throws()
        => Assert.Throws<InvalidOperationException>(
            () => PolicyScheduleValidator.Validate(Cron(ws: 22 * 60, we: 6 * 60)));
}
