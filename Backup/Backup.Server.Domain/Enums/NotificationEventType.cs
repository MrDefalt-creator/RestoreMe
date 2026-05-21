namespace Backup.Server.Domain.Enums;

public enum NotificationEventType
{
    BackupFailed = 0,
    RestoreFailed = 1,
    BackupCompleted = 2,
    AgentOffline = 3,
    AgentBackOnline = 4,
}
