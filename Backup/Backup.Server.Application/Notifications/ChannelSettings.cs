namespace Backup.Server.Application.Notifications;

/// <summary>
/// Strongly-typed projections of NotificationChannel.Settings JSON.
/// Each shape lines up 1:1 with one channel type. Property names use
/// camelCase to match the JSON the frontend sends and the DB stores.
/// </summary>
public sealed record GenericWebhookSettings(string Url, string? Secret);

public sealed record TelegramSettings(string BotToken, string ChatId);

public sealed record SlackSettings(string WebhookUrl);

public sealed record DiscordSettings(string WebhookUrl);
