using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs;

public record HeartBeatRequest(
    [Required] Guid AgentId
    );