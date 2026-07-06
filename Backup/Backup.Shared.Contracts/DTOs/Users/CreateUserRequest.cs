using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Users;

public record CreateUserRequest(
    [Required][StringLength(100)] string Username,
    [Required][StringLength(200)] string Password,
    [Required][StringLength(20)] string Role);
