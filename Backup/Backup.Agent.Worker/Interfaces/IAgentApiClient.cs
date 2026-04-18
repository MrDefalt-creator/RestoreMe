using Backup.Shared.Contracts.DTOs.Agents;
using Backup.Shared.Contracts.DTOs.Policies;

namespace Backup.Agent.Worker.Interfaces;

public interface IAgentApiClient
{
    Task<Guid> RegisterPendingAsync(PendingAgentRequest request, CancellationToken cancellationToken);
    Task<bool> SendHeartbeatAsync(Guid agentId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BackupPolicyDto>> GetPoliciesAsync(Guid agentId, CancellationToken cancellationToken);
    Task<PendingAgentStatusResponse> GetPendingStatusAsync(Guid agentId, CancellationToken cancellationToken);
}
