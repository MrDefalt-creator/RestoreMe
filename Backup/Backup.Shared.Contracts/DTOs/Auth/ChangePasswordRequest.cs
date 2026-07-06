using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Auth;

public record ChangePasswordRequest(
    [Required][StringLength(200)] string CurrentPassword,
    [Required][StringLength(200)] string NewPassword);
