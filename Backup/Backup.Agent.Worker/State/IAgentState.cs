namespace Backup.Agent.Worker.State;

public interface IAgentState
{
    Task<Guid?> TryGetAgentIdAsync(CancellationToken cancellationToken);
    Task SaveAgentIdAsync(Guid agentId, CancellationToken cancellationToken);
}