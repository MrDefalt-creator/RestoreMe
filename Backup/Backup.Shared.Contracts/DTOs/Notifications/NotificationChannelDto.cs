namespace Backup.Shared.Contracts.DTOs.Notifications;

/// <summary>
/// Public-safe view of a notification channel. Excludes the Settings
/// JSON because every channel type stashes at least one secret there
/// (bot tokens, webhook URLs, HMAC secrets) — the admin UI re-enters
/// secrets explicitly when editing instead of pulling them back.
/// </summary>
public sealed record NotificationChannelDto(
    Guid Id,
    string Name,
    string Type,
    bool IsEnabled,
    IReadOnlyList<string> SubscribedEvents,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record CreateNotificationChannelRequest(
    string Name,
    string Type,
    bool IsEnabled,
    string Settings,
    IReadOnlyList<string>? SubscribedEvents);

public sealed record UpdateNotificationChannelRequest(
    string Name,
    bool IsEnabled,
    string? Settings,
    IReadOnlyList<string>? SubscribedEvents);

public sealed record TestNotificationChannelResponse(bool Success, string? Error);
