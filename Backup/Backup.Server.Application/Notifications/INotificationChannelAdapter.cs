using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Application.Notifications;

/// <summary>
/// One adapter per channel kind. Adapters resolve their channel-specific
/// Settings JSON, format the event for the target platform, and POST it
/// over HTTP. They never throw on transport errors (timeouts, non-2xx,
/// malformed config) — wrap and return a failed DeliveryResult instead.
/// </summary>
public interface INotificationChannelAdapter
{
    NotificationChannelType ChannelType { get; }

    Task<DeliveryResult> SendAsync(NotificationChannel channel, NotificationEvent evt, CancellationToken cancellationToken = default);
}
