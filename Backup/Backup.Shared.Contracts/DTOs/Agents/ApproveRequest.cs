using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Agents;

public record ApproveRequest(
    [Required] string Name
    );