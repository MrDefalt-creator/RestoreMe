using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Application.Services;

public class AgentService
{
    private readonly IAgentRepository _agentRepository;
    public AgentService(IAgentRepository agentRepository)
    {
        _agentRepository = agentRepository;
    }
    public async Task<Guid> RegisterAgent(string agentName, string hostName, string os, string version)
    {
        var existingAgent = await _agentRepository.GetByMachineNameAsync(hostName);
        if (existingAgent == null)
        {
            throw new Exception("Agent already exists");
        }

        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            Name = agentName,
            OsType = os,
            Version = version,
        };
        
        await _agentRepository.AddAgent(agent);
        await _agentRepository.SaveChangesAsync();
        
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
        
        _agentRepository.UpdateAgent(agent);
        await _agentRepository.SaveChangesAsync();
    }
}