namespace Backup.Server.Infrastructure.Options;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";
    public string? FailureWebhookUrl { get; init; }
}
