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

    public async Task<List<string>> DeleteAgentWithCascadeAsync(Guid agentId, CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var agent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Agent {agentId} not found.");

        // Collect MinIO object keys before the cascade nukes the rows so the
        // caller can fire best-effort storage cleanup after we commit.
        var objectKeys = await _dbContext.BackupArtifacts
            .Where(a => a.Job.Policy.AgentId == agentId)
            .Select(a => a.ObjectKey)
            .ToListAsync(cancellationToken);

        var artifactIds = await _dbContext.BackupArtifacts
            .Where(a => a.Job.Policy.AgentId == agentId)
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);

        // RestoreJob.Artifact uses OnDelete(Restrict) (see
        // RestoreJobConfiguration), so the artifact cascade below would fail
        // on FK violation unless we manually drop the restore-job rows that
        // reference any of this agent's artifacts first.
        if (artifactIds.Count > 0)
        {
            await _dbContext.RestoreJobs
                .Where(r => artifactIds.Contains(r.ArtifactId))
                .ExecuteDeleteAsync(cancellationToken);
        }

        // RestoreJob.AgentId has no FK relationship, so cascades from Agent
        // delete don't reach it. Wipe by agent id directly.
        await _dbContext.RestoreJobs
            .Where(r => r.AgentId == agentId)
            .ExecuteDeleteAsync(cancellationToken);

        _dbContext.Agents.Remove(agent);
        // SaveChanges here flushes the agent removal (which cascades into
        // Policies → BackupJobs → BackupArtifacts) together with any other
        // tracked changes in this DbContext, e.g. the audit log row the
        // service added before calling us.
        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return objectKeys;
    }
}
