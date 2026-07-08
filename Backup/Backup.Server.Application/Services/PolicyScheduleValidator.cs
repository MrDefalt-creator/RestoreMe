using Backup.Server.Domain.Enums;

namespace Backup.Server.Application.Services;

/// <summary>
/// Raw schedule fields exactly as an API request carries them.
/// A null ScheduleKind means "interval" (legacy clients).
/// </summary>
public sealed record PolicyScheduleInput(
    string? ScheduleKind,
    int? IntervalSeconds,
    string? CronExpression,
    string? TimeZoneId,
    int? WindowStartMinutes,
    int? WindowEndMinutes);

/// <summary>Normalized, validated schedule ready to be copied onto a BackupPolicy.</summary>
public sealed record PolicySchedule(
    ScheduleKind Kind,
    int IntervalSeconds,
    string? CronExpression,
    string? TimeZoneId,
    int? WindowStartMinutes,
    int? WindowEndMinutes);

public static class PolicyScheduleValidator
{
    public static PolicySchedule Validate(PolicyScheduleInput input)
    {
        var kind = (input.ScheduleKind?.Trim().ToLowerInvariant()) switch
        {
            null or "" or "interval" => ScheduleKind.Interval,
            "cron" => ScheduleKind.Cron,
            var other => throw new InvalidOperationException($"Unsupported schedule kind '{other}'.")
        };

        return kind == ScheduleKind.Cron ? ValidateCron(input) : ValidateInterval(input);
    }

    private static PolicySchedule ValidateCron(PolicyScheduleInput input)
    {
        if (input.WindowStartMinutes.HasValue || input.WindowEndMinutes.HasValue)
        {
            throw new InvalidOperationException(
                "Backup windows apply to interval schedules only — the cron expression already encodes the run time.");
        }

        var expression = input.CronExpression?.Trim();
        if (string.IsNullOrEmpty(expression))
        {
            throw new InvalidOperationException("Cron expression is required for cron schedules.");
        }

        try
        {
            Cronos.CronExpression.Parse(expression);
        }
        catch (Cronos.CronFormatException ex)
        {
            throw new InvalidOperationException($"Invalid cron expression '{expression}': {ex.Message}");
        }

        var timeZoneId = RequireTimeZone(input.TimeZoneId, "cron schedules");

        return new PolicySchedule(ScheduleKind.Cron, 0, expression, timeZoneId, null, null);
    }

    private static PolicySchedule ValidateInterval(PolicyScheduleInput input)
    {
        if (input.IntervalSeconds is not int seconds || seconds <= 0)
        {
            throw new InvalidOperationException("Policy interval must be greater than zero seconds.");
        }

        var hasStart = input.WindowStartMinutes.HasValue;
        var hasEnd = input.WindowEndMinutes.HasValue;
        if (hasStart != hasEnd)
        {
            throw new InvalidOperationException("Backup window requires both a start and an end time.");
        }

        string? timeZoneId = null;
        if (hasStart)
        {
            var start = input.WindowStartMinutes!.Value;
            var end = input.WindowEndMinutes!.Value;
            if (start is < 0 or > 1439 || end is < 0 or > 1439)
            {
                throw new InvalidOperationException("Backup window times must be within 00:00–23:59.");
            }

            if (start == end)
            {
                throw new InvalidOperationException("Backup window start and end must differ.");
            }

            timeZoneId = RequireTimeZone(input.TimeZoneId, "backup windows");
        }

        return new PolicySchedule(
            ScheduleKind.Interval, seconds, null, timeZoneId,
            input.WindowStartMinutes, input.WindowEndMinutes);
    }

    private static string RequireTimeZone(string? timeZoneId, string what)
    {
        timeZoneId = timeZoneId?.Trim();
        if (string.IsNullOrEmpty(timeZoneId))
        {
            throw new InvalidOperationException($"An IANA timezone is required for {what}.");
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new InvalidOperationException($"Unknown timezone '{timeZoneId}'.");
        }

        return timeZoneId;
    }
}
