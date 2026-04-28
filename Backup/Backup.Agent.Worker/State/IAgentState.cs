namespace Backup.Agent.Worker.State;

public interface IAgentState
{
    Task<Guid?> TryGetAgentIdAsync(CancellationToken cancellationToken);
    Task<string?> TryGetServerAddressAsync(CancellationToken cancellationToken);
    Task<string?> TryGetAccessTokenAsync(CancellationToken cancellationToken);
    Task SaveAgentIdAsync(Guid agentId, CancellationToken cancellationToken);
    Task SaveServerAddressAsync(string serverAddress, CancellationToken cancellationToken);
    Task SaveAccessTokenAsync(string accessToken, CancellationToken cancellationToken);
}
