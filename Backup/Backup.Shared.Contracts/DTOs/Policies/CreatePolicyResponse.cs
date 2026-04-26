namespace Backup.Shared.Contracts.DTOs.Policies;

public record CreatePolicyResponse(
    Guid PolicyId,
    string Name,
    Guid AgentId
    );