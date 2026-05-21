using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IAgentRepository
{
    Task<List<Agent>> GetAllAgentsAsync();
    Task<Agent?> GetByMachineNameAsync(string machineName);
    Task AddAgent(Agent agent);
    Task SaveChangesAsync();
    Task<Agent?> GetAgentByIdAsync(Guid agentId);
    Task UpdateAgent(Agent agent);
    Task<int?> GetTokenVersionAsync(Guid agentId);

    /// <summary>
    /// Deletes the agent together with everything that references it
    /// (policies → jobs → artifacts, and any restore jobs that point at
    /// either this agent or its artifacts) in a single transaction.
    /// Returns the MinIO object keys of the deleted artifacts so the caller
    /// can fire best-effort storage cleanup after the DB commit.
    /// </summary>
    Task<List<string>> DeleteAgentWithCascadeAsync(Guid agentId, CancellationToken cancellationToken);
}
