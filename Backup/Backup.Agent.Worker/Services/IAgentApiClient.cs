using Backup.Shared.Contracts.DTOs;

namespace Backup.Agent.Worker.Services;

public interface IAgentApiClient
{
    Task<bool> SendHeartbeatAsync(Guid agentId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<BackupPolicyDto>> GetPoliciesAsync(Guid agentId, CancellationToken cancellationToken);
}
