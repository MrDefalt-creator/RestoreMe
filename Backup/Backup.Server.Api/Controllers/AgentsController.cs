using Backup.Server.Api.Security;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Agents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly AgentService _agentService;
    private readonly TokenService _tokenService;

    public AgentsController(AgentService agentService, TokenService tokenService)
    {
        _agentService = agentService;
        _tokenService = tokenService;
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet]
    public async Task<IActionResult> GetAgents()
    {
        var agents = await _agentService.GetAllAgents();
        return Ok(agents.Select(MapAgent));
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("agent/{agentId:guid}")]
    public async Task<IActionResult> GetAgent([FromRoute] Guid agentId)
    {
        var agent = await _agentService.GetAgentById(agentId);
        return Ok(MapAgent(agent));
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingAgents()
    {
        var pendingAgents = await _agentService.GetPendingAgents();
        return Ok(pendingAgents.Select(MapPendingAgent));
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [HttpPost("heartbeat/{agentId:guid}")]
    public async Task<IActionResult> Heartbeat([FromRoute] Guid agentId)
    {
        if (User.TryGetAgentId() != agentId)
        {
            return Forbid();
        }

        await _agentService.Heartbeat(agentId);
        return Ok();
    }

    [Authorize(Policy = AuthConstants.AgentEnrollmentPolicy)]
    [HttpGet("status/{pendingId:guid}")]
    public async Task<IActionResult> Status([FromRoute] Guid pendingId)
    {
        var pendingAgent = await _agentService.GetStatus(pendingId);
        string? agentToken = null;

        if (pendingAgent.Status == Domain.Enums.PendingAgentStatus.Approved &&
            pendingAgent.ApprovedAgentId.HasValue)
        {
            var approvedAgent = await _agentService.GetAgentById(pendingAgent.ApprovedAgentId.Value);
            agentToken = _tokenService.CreateAgentToken(approvedAgent);
        }

        return Ok(new PendingAgentStatusResponse(
            Convert.ToInt32(pendingAgent.Status),
            pendingAgent.ApprovedAgentId,
            agentToken));
    }

    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    [HttpPost("approve/{pendingId:guid}")]
    public async Task<IActionResult> Approve([FromRoute] Guid pendingId, [FromBody] ApproveRequest request)
    {
        var agentId = await _agentService.ApproveAgent(pendingId, request.Name);
        return Ok(agentId);
    }

    [Authorize(Policy = AuthConstants.AgentEnrollmentPolicy)]
    [HttpPost("issue_access_token/{agentId:guid}")]
    public async Task<IActionResult> IssueAccessToken([FromRoute] Guid agentId, [FromBody] IssueAgentAccessTokenRequest request)
    {
        var agent = await _agentService.GetAgentById(agentId);
        if (!string.Equals(agent.MachineName, request.MachineName, StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        return Ok(new IssueAgentAccessTokenResponse(_tokenService.CreateAgentToken(agent)));
    }

    [Authorize(Policy = AuthConstants.AgentEnrollmentPolicy)]
    [HttpPost("register_pending")]
    public async Task<IActionResult> RegisterPending([FromBody] PendingAgentRequest request)
    {
        var pendingId = await _agentService.RegisterPending(request.MachineName, request.OsType, request.OsVersion);
        return Ok(new PendingAgentRegisterResponse(pendingId));
    }

    private AgentListItemDto MapAgent(Agent agent)
    {
        return new AgentListItemDto(
            agent.Id,
            agent.Name,
            agent.MachineName,
            agent.OsType,
            agent.Version,
            _agentService.GetConnectivityStatus(agent),
            agent.CreatedAt,
            agent.LastSeenAt);
    }

    private static PendingAgentListItemDto MapPendingAgent(PendingAgent pendingAgent)
    {
        return new PendingAgentListItemDto(
            pendingAgent.Id,
            pendingAgent.MachineName,
            pendingAgent.OsType,
            pendingAgent.Version,
            pendingAgent.Status.ToString().ToLowerInvariant(),
            pendingAgent.CreatedAt,
            pendingAgent.ApprovedAgentId);
    }
}
