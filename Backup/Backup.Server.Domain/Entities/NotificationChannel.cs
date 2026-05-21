using Backup.Server.Domain.Enums;

namespace Backup.Server.Domain.Entities;

public class NotificationChannel
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public NotificationChannelType Type { get; set; }

    public bool IsEnabled { get; set; } = true;

    // Per-type configuration serialized as JSON. The whole payload is
    // encrypted at rest via DataProtection (configured in AppDbContext)
    // because every channel type carries at least one secret —
    // bot tokens, webhook URLs, shared HMAC secrets.
    public string Settings { get; set; } = "{}";

    // Comma-separated list of NotificationEventType names this channel
    // wants to receive. NULL means "every event" (default for newly
    // created channels), making the upgrade path from a single global
    // webhook trivial — no per-event opt-in required.
    public string? SubscribedEvents { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
