using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Agents;

public record HeartBeatRequest(
    [Required] Guid AgentId
    );