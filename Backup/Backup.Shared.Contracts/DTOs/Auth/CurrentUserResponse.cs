namespace Backup.Shared.Contracts.DTOs.Auth;

public record CurrentUserResponse(
    Guid Id,
    string Username,
    string Role);
