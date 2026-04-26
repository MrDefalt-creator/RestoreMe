using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Agents;

public record RegisterAgentRequest(
    [Required] string AgentName,
    [Required] string Hostname,
    [Required] string OsType,
    [Required] string Version
    );