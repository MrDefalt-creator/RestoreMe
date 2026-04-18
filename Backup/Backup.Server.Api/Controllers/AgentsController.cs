using Backup.Server.Application.Services;
using Backup.Shared.Contracts.DTOs;
using Backup.Shared.Contracts.DTOs.Agents;
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
    

    [HttpPost("heartbeat/{agentId}")]
    public async Task<IActionResult> Heartbeat([FromRoute] Guid agentId)
    {
        await  _agentService.Heartbeat(agentId);
        return Ok();
    }

    [HttpGet("status/{pendingId}")]
    public async Task<IActionResult> Status([FromRoute] Guid pendingId)
    {
        var pendingAgent = await _agentService.GetStatus(pendingId);

        return Ok(new PendingAgentStatusResponse(
            Convert.ToInt32(pendingAgent.Status),
            pendingAgent.ApprovedAgentId));
    }

    [HttpPost("approve/{pendingId}")]
    public async Task<IActionResult> Approve([FromRoute] Guid pendingId, [FromBody] ApproveRequest request)
    {
        var agentId = await _agentService.ApproveAgent(pendingId, request.Name);
        return Ok(agentId);
    }

    [HttpPost("register_pending")]
    public async Task<IActionResult> RegisterPending([FromBody] PendingAgentRequest request)
    {
        var pendingId = await _agentService.RegisterPending(request.MachineName, request.OsType, request.OsVersion);
        return Ok(new PendingAgentRegisterResponse(pendingId));
    }
    
}