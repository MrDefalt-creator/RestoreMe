using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs;

public record PendingAgentRequest(
    [Required] string MachineName,
    [Required] string OsType,
    [Required] string OsVersion
    );