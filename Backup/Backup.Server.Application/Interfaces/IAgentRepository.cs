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
}
