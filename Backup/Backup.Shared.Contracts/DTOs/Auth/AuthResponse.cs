namespace Backup.Shared.Contracts.DTOs.Auth;

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    CurrentUserResponse User);
