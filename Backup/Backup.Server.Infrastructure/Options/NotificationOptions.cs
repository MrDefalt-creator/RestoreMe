namespace Backup.Server.Infrastructure.Options;

public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";
    public string? FailureWebhookUrl { get; init; }

    // Shared secret used to HMAC-SHA256 the request body. When set, the
    // receiver can verify authenticity by recomputing the digest over the
    // raw body and constant-time comparing with the X-RestoreMe-Signature
    // header (format: "sha256=<hex>").
    public string? WebhookSecret { get; init; }
}
