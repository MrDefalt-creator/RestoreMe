using Backup.Shared.Contracts.DTOs;

namespace Backup.Agent.Worker.Services;

public interface IAgentApiClient
{
    Task<Guid> RegisterPendingAsync(PendingAgentRequest request, CancellationToken cancellationToken);
    Task<bool> SendHeartbeatAsync(Guid agentId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BackupPolicyDto>> GetPoliciesAsync(Guid agentId, CancellationToken cancellationToken);
    Task<PendingAgentStatusResponse> GetPendingStatusAsync(Guid agentId, CancellationToken cancellationToken);
}
