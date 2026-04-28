namespace Backup.Shared.Contracts.DTOs.Users;

public record CreateUserRequest(
    string Username,
    string Password,
    string Role);
