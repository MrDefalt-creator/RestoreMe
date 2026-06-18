using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Configuration;
using Backup.Shared.Contracts.DTOs.Agents;
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

    public async Task<AgentDeletionImpact> GetDeletionImpactAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var policyCount = await _dbContext.BackupPolicies
            .AsNoTracking()
            .CountAsync(p => p.AgentId == agentId, cancellationToken);

        var jobCount = await _dbContext.BackupJobs
            .AsNoTracking()
            .CountAsync(j => j.AgentId == agentId, cancellationToken);

        var artifactQuery = _dbContext.BackupArtifacts
            .AsNoTracking()
            .Where(a => a.Job != null && a.Job.AgentId == agentId);

        var artifactCount = await artifactQuery.CountAsync(cancellationToken);
        var totalBytes = artifactCount == 0
            ? 0L
            : await artifactQuery.SumAsync(a => a.SizeBytes, cancellationToken);

        var restoreCount = await _dbContext.RestoreJobs
            .AsNoTracking()
            .CountAsync(r => r.AgentId == agentId, cancellationToken);

        var pendingRestoreCount = await _dbContext.RestoreJobs
            .AsNoTracking()
            .CountAsync(
                r => r.AgentId == agentId
                    && (r.Status == RestoreJobStatus.Pending || r.Status == RestoreJobStatus.Running),
                cancellationToken);

        return new AgentDeletionImpact(
            policyCount,
            jobCount,
            artifactCount,
            totalBytes,
            restoreCount,
            pendingRestoreCount);
    }

    public async Task<List<string>> DeleteAgentAsync(
        Guid agentId,
        DeleteAgentOptions options,
        CancellationToken cancellationToken)
    {
        await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var agent = await _dbContext.Agents.FirstOrDefaultAsync(a => a.Id == agentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Agent {agentId} not found.");

        if (!options.PurgeRestoreHistory)
        {
            var pending = await _dbContext.RestoreJobs
                .AsNoTracking()
                .AnyAsync(
                    r => r.AgentId == agentId
                        && (r.Status == RestoreJobStatus.Pending || r.Status == RestoreJobStatus.Running),
                    cancellationToken);
            if (pending)
            {
                throw new InvalidOperationException(
                    "Pending or running restore jobs reference this agent. Cancel them first, or enable 'Delete restore history'.");
            }
        }

        // Object keys are collected before any deletion so the API can
        // attempt the best-effort storage cleanup after we commit.
        var storageKeys = options.PurgeStorageFiles && options.PurgeBackupHistory
            ? await _dbContext.BackupArtifacts
                .AsNoTracking()
                .Where(a => a.Job != null && a.Job.AgentId == agentId)
                .Select(a => a.ObjectKey)
                .ToListAsync(cancellationToken)
            : new List<string>();

        // 1. Restore jobs that reference this agent's artifacts / agent id
        if (options.PurgeRestoreHistory)
        {
            await _dbContext.RestoreJobs
                .Where(r => r.AgentId == agentId)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            // Two distinct cases when we want to *keep* restore history:
            //
            // (a) Rows where THIS agent was the executor (r.AgentId ==
            //     agentId). Snapshot the agent display name and null
            //     the AgentId FK.
            //
            // (b) Rows where a *different* agent restored an artifact
            //     that THIS agent owned. AgentId stays untouched
            //     (that's a different machine and its history must
            //     remain accurate); we only snapshot artifact strings
            //     so the row keeps reading meaningfully after the
            //     artifact is gone.
            //
            // Splitting into two queries avoids the original bug where
            // every matched row had its AgentId nulled and its
            // AgentNameSnapshot stamped with the deleted agent's name —
            // corrupting cross-agent restore history.
            var executorRows = await _dbContext.RestoreJobs
                .Where(r => r.AgentId == agentId)
                .Include(r => r.Artifact)
                .ToListAsync(cancellationToken);

            foreach (var row in executorRows)
            {
                row.AgentNameSnapshot ??= agent.Name;
                if (row.Artifact is not null)
                {
                    row.ArtifactFileNameSnapshot ??= row.Artifact.FileName;
                    row.ArtifactObjectKeySnapshot ??= row.Artifact.ObjectKey;
                }
                row.AgentId = null;
                if (options.PurgeBackupHistory)
                {
                    row.ArtifactId = null;
                }
            }

            if (options.PurgeBackupHistory)
            {
                var artifactDependentRows = await _dbContext.RestoreJobs
                    .Where(r => (r.AgentId == null || r.AgentId != agentId)
                        && r.Artifact != null
                        && r.Artifact.Job != null
                        && r.Artifact.Job.AgentId == agentId)
                    .Include(r => r.Artifact)
                    .ToListAsync(cancellationToken);

                foreach (var row in artifactDependentRows)
                {
                    if (row.Artifact is not null)
                    {
                        row.ArtifactFileNameSnapshot ??= row.Artifact.FileName;
                        row.ArtifactObjectKeySnapshot ??= row.Artifact.ObjectKey;
                    }
                    row.ArtifactId = null;
                }
            }
        }

        // 2. Backup jobs and their artifacts
        if (options.PurgeBackupHistory)
        {
            // Cascade via Agent → Policy → BackupJob → BackupArtifact
            // happens on Remove(agent) below. We also wipe any jobs that
            // referenced this agent directly via Job.AgentId (they would
            // SetNull otherwise and become orphans we don't want).
            await _dbContext.BackupJobs
                .Where(j => j.AgentId == agentId)
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            // Detach jobs from the agent, snapshotting display names
            // before the FK goes null.
            var detachedJobs = await _dbContext.BackupJobs
                .Where(j => j.AgentId == agentId)
                .Include(j => j.Policy)
                .ToListAsync(cancellationToken);

            foreach (var job in detachedJobs)
            {
                job.AgentNameSnapshot ??= agent.Name;
                job.PolicyNameSnapshot ??= job.Policy?.Name;
                job.AgentId = null;
                // Policies cascade-delete with the agent, so we must also
                // unhook the job from its policy before that happens.
                job.PolicyId = null;
            }
        }

        // 3. The agent itself (and its policies, via Agent → Policy
        //    cascade). Job and RestoreJob FKs were either purged or
        //    nulled above so the cascade stays scoped.
        _dbContext.Agents.Remove(agent);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return storageKeys;
    }
}
