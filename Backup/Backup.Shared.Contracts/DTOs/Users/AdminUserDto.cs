namespace Backup.Shared.Contracts.DTOs.Users;

public record AdminUserDto(
    Guid Id,
    string Username,
    string Role,
    bool IsActive,
    DateTime CreatedAtUtc);
