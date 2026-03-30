namespace Backup.Shared.Contracts.DTOs;

public record CreatePolicyResponse(
    Guid PolicyId,
    string Name,
    Guid AgentId
    );