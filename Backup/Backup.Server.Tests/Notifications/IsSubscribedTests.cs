using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Tests.Notifications;

public class IsSubscribedTests
{
    [Theory]
    [InlineData(null, NotificationEventType.BackupFailed, true)]
    [InlineData("", NotificationEventType.BackupFailed, true)]
    [InlineData("   ", NotificationEventType.BackupFailed, true)]
    public void Empty_subscription_matches_every_event(string? subscribed, NotificationEventType eventType, bool expected)
    {
        var channel = new NotificationChannel { SubscribedEvents = subscribed };
        Assert.Equal(expected, NotificationDispatcher.IsSubscribed(channel, eventType));
    }

    [Theory]
    [InlineData("BackupFailed", NotificationEventType.BackupFailed, true)]
    [InlineData("backupfailed", NotificationEventType.BackupFailed, true)]
    [InlineData("BackupFailed,RestoreFailed", NotificationEventType.RestoreFailed, true)]
    [InlineData("BackupFailed,RestoreFailed", NotificationEventType.BackupCompleted, false)]
    [InlineData("BackupCompleted", NotificationEventType.BackupFailed, false)]
    public void Csv_subscription_is_filter(string subscribed, NotificationEventType eventType, bool expected)
    {
        var channel = new NotificationChannel { SubscribedEvents = subscribed };
        Assert.Equal(expected, NotificationDispatcher.IsSubscribed(channel, eventType));
    }

    [Theory]
    [InlineData("0", NotificationEventType.BackupFailed, true)]
    [InlineData("2,3", NotificationEventType.AgentOffline, true)]
    [InlineData("2,3", NotificationEventType.BackupFailed, false)]
    public void Tolerates_numeric_tokens(string subscribed, NotificationEventType eventType, bool expected)
    {
        var channel = new NotificationChannel { SubscribedEvents = subscribed };
        Assert.Equal(expected, NotificationDispatcher.IsSubscribed(channel, eventType));
    }

    [Fact]
    public void Trims_whitespace_around_tokens()
    {
        var channel = new NotificationChannel { SubscribedEvents = " BackupFailed , RestoreFailed " };
        Assert.True(NotificationDispatcher.IsSubscribed(channel, NotificationEventType.BackupFailed));
        Assert.True(NotificationDispatcher.IsSubscribed(channel, NotificationEventType.RestoreFailed));
    }
}
