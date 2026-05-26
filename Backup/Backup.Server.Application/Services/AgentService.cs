using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Backup.Server.Application.Services;

public class AgentService
{
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(3);

    private readonly IAgentRepository _agentRepository;
    private readonly IPendingAgentsRepository _pendingAgentsRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMemoryCache _cache;

    public AgentService(
        IAgentRepository agentRepository,
        IPendingAgentsRepository pendingAgentsRepository,
        IAuditLogRepository auditLogRepository,
        IMemoryCache cache)
    {
        _agentRepository = agentRepository;
        _pendingAgentsRepository = pendingAgentsRepository;
        _auditLogRepository = auditLogRepository;
        _cache = cache;
    }

    public async Task RevokeAgentTokenAsync(Guid agentId, Guid actorUserId)
    {
        var agent = await _agentRepository.GetAgentByIdAsync(agentId)
            ?? throw new KeyNotFoundException($"Agent {agentId} not found.");

        agent.TokenVersion++;
        await _agentRepository.UpdateAgent(agent);
        await _auditLogRepository.AddAsync(Audit(
            actorUserId,
            "agent.revoke",
            agentId,
            $"machine={agent.MachineName} new_version={agent.TokenVersion}"));
        await _agentRepository.SaveChangesAsync();

        // Invalidate the cached token version so the next request from the
        // revoked agent fails immediately rather than after the 30 s TTL.
        _cache.Remove($"agent-tokver:{agentId}");
    }

    public Task<AgentDeletionImpact> GetDeletionImpactAsync(Guid agentId, CancellationToken cancellationToken)
        => _agentRepository.GetDeletionImpactAsync(agentId, cancellationToken);

    /// <summary>
    /// Removes the agent. The <paramref name="options"/> flags control
    /// whether backup history, MinIO objects, and restore history go with
    /// it. The audit row records the operator's choice in details so
    /// reviewing the log later makes the action self-explanatory. Returns
    /// the MinIO object keys the caller should attempt to remove (empty
    /// when "keep files" is on).
    /// </summary>
    public async Task<List<string>> DeleteAgentAsync(
        Guid agentId,
        Guid actorUserId,
        DeleteAgentOptions options,
        CancellationToken cancellationToken)
    {
        var agent = await _agentRepository.GetAgentByIdAsync(agentId)
            ?? throw new KeyNotFoundException($"Agent {agentId} not found.");

        // Queue the audit row on the same DbContext before the repo's
        // SaveChanges inside the transaction — that way the audit insert and
        // the agent removal commit (or roll back) together.
        await _auditLogRepository.AddAsync(Audit(
            actorUserId,
            "agent.deleted",
            agentId,
            $"machine={agent.MachineName} name={agent.Name} {FormatPurgeFlags(options)}"));

        var storageKeys = await _agentRepository.DeleteAgentAsync(agentId, options, cancellationToken);

        // Drop the cached token version so any in-flight request from this
        // agent fails at auth time instead of after the cache TTL.
        _cache.Remove($"agent-tokver:{agentId}");

        return storageKeys;
    }

    private static string FormatPurgeFlags(DeleteAgentOptions options)
    {
        return $"purged_history={options.PurgeBackupHistory} purged_files={options.PurgeStorageFiles} purged_restores={options.PurgeRestoreHistory}";
    }

    public async Task<Guid> RegisterPending(string machineName, string os, string version)
    {
        var existingAgent = await _agentRepository.GetByMachineNameAsync(machineName);

        if (existingAgent != null)
        {
            throw new InvalidOperationException("Agent already exists");
        }

        var existingPending = await _pendingAgentsRepository.GetByMachineNameAsync(machineName);
        if (existingPending != null)
        {
            if (existingPending.Status == PendingAgentStatus.Rejected)
            {
                existingPending.OsType = os;
                existingPending.Version = version;
                existingPending.Status = PendingAgentStatus.Pending;
                existingPending.CreatedAt = DateTime.UtcNow;
                existingPending.ApprovedAt = null;
                existingPending.ApprovedAgentId = null;

                await _pendingAgentsRepository.UpdateAsync(existingPending);
                await _pendingAgentsRepository.SaveChangesAsync();
            }

            return existingPending.Id;
        }

        var pendingAgent = new PendingAgent
        {
            Id = Guid.NewGuid(),
            MachineName = machineName,
            OsType = os,
            Version = version,
            Status = PendingAgentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _pendingAgentsRepository.AddAsync(pendingAgent);
        await _pendingAgentsRepository.SaveChangesAsync();

        return pendingAgent.Id;
    }

    public async Task<PendingAgent> GetStatus(Guid pendingId)
    {
        var agent = await _pendingAgentsRepository.GetByIdAsync(pendingId);
        return agent ?? throw new KeyNotFoundException($"Pending agent {pendingId} not found.");
    }

    public async Task<List<Agent>> GetAllAgents()
    {
        return await _agentRepository.GetAllAgentsAsync();
    }

    public async Task<Agent> GetAgentById(Guid agentId)
    {
        var agent = await _agentRepository.GetAgentByIdAsync(agentId);
        return agent ?? throw new KeyNotFoundException($"Agent {agentId} not found.");
    }

    public async Task<List<PendingAgent>> GetPendingAgents()
    {
        return await _pendingAgentsRepository.GetPendingAgentsAsync();
    }

    public async Task<Guid> ApproveAgent(Guid pendingId, string name, Guid actorId)
    {
        var pendingAgent = await _pendingAgentsRepository.GetByIdAsync(pendingId)
            ?? throw new KeyNotFoundException($"Pending agent {pendingId} not found.");

        if (pendingAgent.Status == PendingAgentStatus.Approved && pendingAgent.ApprovedAgentId.HasValue)
        {
            return pendingAgent.ApprovedAgentId.Value;
        }

        var existingAgent = await _agentRepository.GetByMachineNameAsync(pendingAgent.MachineName);
        if (existingAgent != null)
        {
            pendingAgent.Status = PendingAgentStatus.Approved;
            pendingAgent.ApprovedAgentId = existingAgent.Id;
            pendingAgent.ApprovedAt = DateTime.UtcNow;

            await _pendingAgentsRepository.UpdateAsync(pendingAgent);
            await _auditLogRepository.AddAsync(Audit(actorId, "agent.approve", existingAgent.Id, $"machine={pendingAgent.MachineName}"));
            await _pendingAgentsRepository.SaveChangesAsync();

            return existingAgent.Id;
        }

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            MachineName = pendingAgent.MachineName,
            Name = name,
            OsType = pendingAgent.OsType,
            Version = pendingAgent.Version,
            Status = AgentStatus.Offline,
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow
        };

        await _agentRepository.AddAgent(agent);
        await _agentRepository.SaveChangesAsync();

        pendingAgent.Status = PendingAgentStatus.Approved;
        pendingAgent.ApprovedAgentId = agent.Id;
        pendingAgent.ApprovedAt = DateTime.UtcNow;

        await _pendingAgentsRepository.UpdateAsync(pendingAgent);
        await _auditLogRepository.AddAsync(Audit(actorId, "agent.approve", agent.Id, $"machine={pendingAgent.MachineName} name={name}"));
        await _pendingAgentsRepository.SaveChangesAsync();

        return agent.Id;
    }

    // Install-token enrollment path: the admin already authorised this
    // specific agent by minting a single-use token in the UI, so we skip
    // the pending-approval queue entirely. Returns the freshly-created
    // approved agent, or throws if the requested machine name collides
    // with an existing agent (the install token is left consumed so the
    // operator regenerates a new one rather than retrying silently).
    public async Task<Agent> ApproveFromInstallTokenAsync(
        string machineName,
        string osType,
        string osVersion,
        string? preApprovedName,
        Guid createdByUserId,
        Guid installTokenId)
    {
        var existing = await _agentRepository.GetByMachineNameAsync(machineName);
        if (existing != null)
        {
            throw new InvalidOperationException(
                $"An agent with machine name '{machineName}' already exists.");
        }

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            MachineName = machineName,
            Name = string.IsNullOrWhiteSpace(preApprovedName) ? machineName : preApprovedName,
            OsType = osType,
            Version = osVersion,
            Status = AgentStatus.Offline,
            CreatedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow,
        };

        await _agentRepository.AddAgent(agent);
        await _auditLogRepository.AddAsync(Audit(
            createdByUserId,
            "agent.install_token.used",
            installTokenId,
            $"machine={machineName} agent={agent.Id}"));
        await _auditLogRepository.AddAsync(Audit(
            createdByUserId,
            "agent.approve",
            agent.Id,
            $"machine={machineName} name={agent.Name} via=install_token"));
        await _agentRepository.SaveChangesAsync();

        return agent;
    }

    public async Task RejectAgent(Guid pendingId, Guid actorId)
    {
        var pendingAgent = await _pendingAgentsRepository.GetByIdAsync(pendingId)
            ?? throw new KeyNotFoundException($"Pending agent {pendingId} not found.");

        if (pendingAgent.Status == PendingAgentStatus.Approved)
        {
            throw new InvalidOperationException("Approved agent requests cannot be rejected.");
        }

        pendingAgent.Status = PendingAgentStatus.Rejected;
        pendingAgent.ApprovedAgentId = null;
        pendingAgent.ApprovedAt = null;

        await _pendingAgentsRepository.UpdateAsync(pendingAgent);
        await _auditLogRepository.AddAsync(Audit(actorId, "agent.reject", pendingAgent.Id, $"machine={pendingAgent.MachineName}"));
        await _pendingAgentsRepository.SaveChangesAsync();
    }

    public async Task Heartbeat(Guid agentId)
    {
        var agent = await _agentRepository.GetAgentByIdAsync(agentId)
            ?? throw new KeyNotFoundException($"Agent {agentId} not found.");

        agent.LastSeenAt = DateTime.UtcNow;

        await _agentRepository.UpdateAgent(agent);
        await _agentRepository.SaveChangesAsync();
    }

    public string GetConnectivityStatus(Agent agent, DateTime? utcNow = null)
    {
        if (!agent.LastSeenAt.HasValue)
        {
            return "offline";
        }

        var elapsed = (utcNow ?? DateTime.UtcNow) - agent.LastSeenAt.Value;

        if (elapsed < OnlineThreshold)
        {
            return "online";
        }

        if (elapsed < StaleThreshold)
        {
            return "stale";
        }

        return "offline";
    }

    private static AuditLog Audit(Guid actorId, string action, Guid? targetId = null, string? details = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Action = action,
            TargetId = targetId,
            Details = details,
            OccurredAt = DateTime.UtcNow
        };
}
