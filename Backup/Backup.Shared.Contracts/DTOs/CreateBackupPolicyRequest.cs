using System.ComponentModel.DataAnnotations;

namespace Backup.Shared.Contracts.DTOs;

public record CreateBackupPolicyRequest(
    
    [Required][StringLength(150)] string Name,
    [Required][StringLength(500)] string SourcePath
    
    );