using Backup.Server.Domain.Enums;

namespace Backup.Server.Application.Notifications;

/// <summary>
/// Channel-neutral event payload handed off to every adapter. The
/// dispatcher builds it once per signal; each adapter formats it for
/// its target chat platform without re-querying the database.
/// </summary>
public sealed record NotificationEvent(
    NotificationEventType Type,
    string Title,
    string Summary,
    string? Detail,
    DateTime OccurredAt,
    IReadOnlyDictionary<string, string?> Metadata);
