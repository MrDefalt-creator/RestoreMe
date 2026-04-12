using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs;

public record ApproveRequest(
    [Required] string Name
    );