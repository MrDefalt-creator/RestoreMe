namespace Backup.Server.Application.Notifications;

/// <summary>
/// Outcome of a single adapter send. Adapters never throw on transport
/// errors — they return Success=false with a short error description so
/// the dispatcher can audit the result without disrupting fan-out.
/// </summary>
public sealed record DeliveryResult(bool Success, string? Error)
{
    public static DeliveryResult Ok() => new(true, null);
    public static DeliveryResult Failure(string error) => new(false, error);
}
