namespace Backup.Shared.Contracts.DTOs.Auth;

// One active refresh-token lineage = one signed-in device/session. Current
// marks the session whose refresh cookie the caller presented.
public record SessionDto(
    Guid Id,
    DateTime CreatedAtUtc,
    DateTime? LastUsedAtUtc,
    string? UserAgent,
    string? CreatedByIp,
    bool Current);
