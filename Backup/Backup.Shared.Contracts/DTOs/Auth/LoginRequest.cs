using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Auth;

public record LoginRequest(
    [Required][StringLength(100)] string Username,
    [Required][StringLength(200)] string Password,
    bool RememberMe = false);
