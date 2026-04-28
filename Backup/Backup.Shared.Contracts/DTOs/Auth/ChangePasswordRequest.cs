namespace Backup.Shared.Contracts.DTOs.Auth;

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword);
