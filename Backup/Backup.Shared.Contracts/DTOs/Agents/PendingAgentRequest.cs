using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs.Agents;

public record PendingAgentRequest(
    [Required] string MachineName,
    [Required] string OsType,
    [Required] string OsVersion
    );