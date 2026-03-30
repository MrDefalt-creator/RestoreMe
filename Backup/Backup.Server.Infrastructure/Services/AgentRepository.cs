using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class AgentRepository : IAgentRepository
{
    private readonly AppDbContext _dbContext;
    public AgentRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    public async Task<Agent?> GetByMachineNameAsync(string machineName)
    {
        return await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.MachineName == machineName);
    }

    public async Task AddAgent(Agent agent)
    {
        await _dbContext.Agents.AddAsync(agent);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }

    public async Task<Agent?> GetAgentByIdAsync(Guid agentId)
    {
        return await _dbContext.Agents
            .FirstOrDefaultAsync(a => a.Id == agentId);
    }

    public async Task UpdateAgent(Agent agent)
    {
        _dbContext.Agents.Update(agent);
        
    }
}