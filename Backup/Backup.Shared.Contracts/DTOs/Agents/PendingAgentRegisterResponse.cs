namespace Backup.Shared.Contracts.DTOs.Agents;

// PendingId is always set so legacy agent workers that ignore the new
// fields keep working. AgentId + AccessToken are populated only when the
// caller authenticated with a per-agent install token, in which case the
// agent is already approved and can skip the status-polling loop.
public record PendingAgentRegisterResponse(
    Guid PendingId,
    Guid? AgentId = null,
    string? AccessToken = null);
