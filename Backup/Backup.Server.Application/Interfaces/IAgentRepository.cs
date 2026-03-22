using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IAgentRepository
{
    Task<Agent?> GetByMachineNameAsync(string machineName);
    Task AddAgent(Agent agent);
    Task SaveChangesAsync();
    Task<Agent?> GetAgentByIdAsync(Guid agentId);
    void UpdateAgent(Agent agent);
}