using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class PendingAgentsRepository : IPendingAgentsRepository
{
    private readonly AppDbContext _dbContext;

    public PendingAgentsRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    public async Task<List<PendingAgent>> GetPendingAgentsAsync()
    {
        return await _dbContext.PendingAgents
            .AsNoTracking()
            .Where(x => x.Status == PendingAgentStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<PendingAgent?> GetByIdAsync(Guid id)
    {
        return await _dbContext.PendingAgents.FirstOrDefaultAsync(x => x.Id == id);
    }

    public Task<PendingAgent?> GetByMachineNameAsync(string machineName)
    {
        return _dbContext.PendingAgents
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.MachineName == machineName);
    }

    public async Task AddAsync(PendingAgent pendingAgent)
    {
        await _dbContext.PendingAgents.AddAsync(pendingAgent);
    }

    public async Task UpdateAsync(PendingAgent pendingAgent)
    {
        _dbContext.PendingAgents.Update(pendingAgent);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
