using Backup.Server.Application.Services;
using Backup.Shared.Contracts.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;


[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly AgentService _agentService;
    public AgentsController(AgentService agentService)
    {
        _agentService = agentService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterAgentRequest request)
    {
        Guid agentId = await _agentService.RegisterAgent(request.AgentName, request.Hostname, request.OsType, request.Version);
        
        return Ok(agentId);
    }

    [HttpPost("heartbeat/{agentId}")]
    public async Task<IActionResult> Heartbeat([FromRoute] HeartBeatRequest request)
    {
        return Ok();
    }
    
}