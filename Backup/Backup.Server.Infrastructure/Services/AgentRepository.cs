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
    
    public async Task<List<Agent>> GetAllAgentsAsync()
    {
        return await _dbContext.Agents
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();
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

    public async Task<int?> GetTokenVersionAsync(Guid agentId)
    {
        // Project just the version so OnTokenValidated stays a single-row,
        // single-column lookup.
        return await _dbContext.Agents
            .AsNoTracking()
            .Where(a => a.Id == agentId)
            .Select(a => (int?)a.TokenVersion)
            .FirstOrDefaultAsync();
    }
}
