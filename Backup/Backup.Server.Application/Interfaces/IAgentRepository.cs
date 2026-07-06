using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Agents;

namespace Backup.Server.Application.Interfaces;

public interface IAgentRepository
{
    Task<List<Agent>> GetAllAgentsAsync();

    // Sort keys: createdAt (default), name, lastSeenAt, status.
    Task<PagedResult<Agent>> QueryAgentsAsync(PagedQuery query, CancellationToken cancellationToken);
    Task<Agent?> GetByMachineNameAsync(string machineName);
    Task AddAgent(Agent agent);
    Task SaveChangesAsync();
    Task<Agent?> GetAgentByIdAsync(Guid agentId);
    Task UpdateAgent(Agent agent);
    Task<int?> GetTokenVersionAsync(Guid agentId);

    /// <summary>
    /// Counts the rows that depend on an agent so the UI can show the
    /// operator exactly what will disappear (or be detached) before they
    /// hit Delete.
    /// </summary>
    Task<AgentDeletionImpact> GetDeletionImpactAsync(Guid agentId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the agent. The options control whether jobs / artifacts /
    /// restore rows go with it or stay behind as orphaned history rows
    /// (with snapshot strings preserving display names). Returns the
    /// MinIO object keys that the caller should attempt to remove from
    /// storage; for "keep files" the list is empty.
    /// </summary>
    Task<List<string>> DeleteAgentAsync(
        Guid agentId,
        DeleteAgentOptions options,
        CancellationToken cancellationToken);
}
