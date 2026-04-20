using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IPendingAgentsRepository
{
    Task<List<PendingAgent>> GetPendingAgentsAsync();
    Task<PendingAgent?> GetByIdAsync(Guid id);
    Task<PendingAgent?> GetByMachineNameAsync(string machineName);
    Task AddAsync(PendingAgent pendingAgent);
    
    Task UpdateAsync(PendingAgent pendingAgent);
    
    Task SaveChangesAsync();
}
