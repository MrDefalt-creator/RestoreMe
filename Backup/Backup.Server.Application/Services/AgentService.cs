using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Application.Services;

public class AgentService
{
    private readonly IAgentRepository _agentRepository;
    private readonly IPendingAgentsRepository _pendingAgentsRepository;
    public AgentService(IAgentRepository agentRepository, IPendingAgentsRepository pendingAgentsRepository)
    {
        _agentRepository = agentRepository;
        _pendingAgentsRepository = pendingAgentsRepository;
    }
    
    public async Task<Guid> RegisterPending(string machineName, string os, string version)
    {
        var existingAgent = await _agentRepository.GetByMachineNameAsync(machineName);

        if (existingAgent != null)
        {
            throw new Exception("Agent already exists");
        }
    
        var existingPending = await _pendingAgentsRepository.GetByMachineNameAsync(machineName);
        if (existingPending != null)
        {
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

        if (agent == null)
        {
            throw new Exception("Agent not found");
        }
        
        return agent;
    }
    
    public async Task<List<Agent>> GetAllAgents()
    {
        return await _agentRepository.GetAllAgentsAsync();
    }
    
    public async Task<Agent> GetAgentById(Guid agentId)
    {
        var agent = await _agentRepository.GetAgentByIdAsync(agentId);
        if (agent == null)
        {
            throw new Exception("Agent not found");
        }

        return agent;
    }
    
    public async Task<List<PendingAgent>> GetPendingAgents()
    {
        return await _pendingAgentsRepository.GetPendingAgentsAsync();
    }

    public async Task<Guid> ApproveAgent(Guid pendingId, string name)
    {
        var pendingAgent = await _pendingAgentsRepository.GetByIdAsync(pendingId);

        if (pendingAgent == null)
        {
            throw new Exception("Pending agent not found");
        }

        if (pendingAgent.Status == PendingAgentStatus.Approved && pendingAgent.ApprovedAgentId.HasValue)
        {
            return pendingAgent.ApprovedAgentId.Value;
        }

        var existingAgent = await _agentRepository.GetByMachineNameAsync(pendingAgent.MachineName);
        if (existingAgent != null)
        {
            pendingAgent.Status = PendingAgentStatus.Approved;
            pendingAgent.ApprovedAgentId = existingAgent.Id;

            await _pendingAgentsRepository.UpdateAsync(pendingAgent);
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
            CreatedAt = DateTime.UtcNow
        };

        await _agentRepository.AddAgent(agent);
        await _agentRepository.SaveChangesAsync();

        pendingAgent.Status = PendingAgentStatus.Approved;
        pendingAgent.ApprovedAgentId = agent.Id;

        await _pendingAgentsRepository.UpdateAsync(pendingAgent);
        await _pendingAgentsRepository.SaveChangesAsync();

        return agent.Id;
    }


    public async Task Heartbeat(Guid agentId)
    {
        var agent = await _agentRepository.GetAgentByIdAsync(agentId);

        if (agent == null)
        {
            throw new Exception("This agent doesn't exist");
        }
        
        agent.LastSeenAt = DateTime.UtcNow;
        agent.Status = AgentStatus.Online;
        
        await _agentRepository.UpdateAgent(agent);
        await _agentRepository.SaveChangesAsync();
    }
}
