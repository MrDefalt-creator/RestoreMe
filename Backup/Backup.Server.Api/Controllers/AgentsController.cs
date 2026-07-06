using Backup.Server.Api.Security;
using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Options;
using Backup.Shared.Contracts.DTOs.Agents;
using Backup.Shared.Contracts.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
// AgentEnrollmentAuthenticationHandler lives in this namespace

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
    public async Task<IActionResult> GetAgents(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? sortBy,
        [FromQuery] string? sortDir,
        CancellationToken cancellationToken)
    {
        // No pagination params → legacy full-array shape, so older clients
        // (and internal callers) keep working unchanged.
        if (page is null && pageSize is null && sortBy is null)
        {
            var agents = await _agentService.GetAllAgents();
            return Ok(agents.Select(MapAgent));
        }

        var query = PagedQuery.Normalize(page, pageSize, sortBy, sortDir);
        var result = await _agentService.QueryAgents(query, cancellationToken);
        return Ok(new PagedResponse<AgentListItemDto>(
            result.Items.Select(MapAgent).ToList(),
            result.Total,
            query.Page,
            query.PageSize));
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

    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    [HttpGet("enrollment-info")]
    public async Task<IActionResult> GetEnrollmentInfo(
        [FromServices] IOptions<AgentEnrollmentOptions> enrollmentOptions,
        [FromServices] IAuditLogRepository auditLogRepository)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        // Token is a secret. Every read leaves a paper trail so admins can
        // see who pulled the install command — and the install-agent
        // wizard's audit row in the panel is enough to spot abuse.
        await auditLogRepository.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorId = actorUserId.Value,
            Action = "agent.enrollment_info_viewed",
            OccurredAt = DateTime.UtcNow,
        });
        await auditLogRepository.SaveChangesAsync();

        return Ok(new EnrollmentInfoResponse(enrollmentOptions.Value.EnrollmentToken));
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [EnableRateLimiting("agent-write")]
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
    [EnableRateLimiting("enrollment-public")]
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
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var agentId = await _agentService.ApproveAgent(pendingId, request.Name, actorUserId.Value);
        return Ok(agentId);
    }

    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    [HttpPost("reject/{pendingId:guid}")]
    public async Task<IActionResult> Reject([FromRoute] Guid pendingId)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        await _agentService.RejectAgent(pendingId, actorUserId.Value);
        return NoContent();
    }

    [Authorize(Policy = AuthConstants.UserManagementPolicy)]
    [HttpPost("{agentId:guid}/revoke")]
    public async Task<IActionResult> Revoke([FromRoute] Guid agentId)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        await _agentService.RevokeAgentTokenAsync(agentId, actorUserId.Value);
        return NoContent();
    }

    [Authorize(Policy = AuthConstants.UserManagementPolicy)]
    [HttpGet("{agentId:guid}/deletion-impact")]
    public async Task<IActionResult> GetDeletionImpact([FromRoute] Guid agentId)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        try
        {
            var agent = await _agentService.GetAgentById(agentId);
            _ = agent;
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        var impact = await _agentService.GetDeletionImpactAsync(agentId, HttpContext.RequestAborted);
        return Ok(impact);
    }

    [Authorize(Policy = AuthConstants.UserManagementPolicy)]
    [HttpDelete("{agentId:guid}")]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid agentId,
        [FromBody] DeleteAgentOptions? options,
        [FromServices] IStorageAccessService storage,
        [FromServices] ILogger<AgentsController> logger)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var effectiveOptions = options ?? new DeleteAgentOptions();

        List<string> storageKeys;
        try
        {
            storageKeys = await _agentService.DeleteAgentAsync(
                agentId,
                actorUserId.Value,
                effectiveOptions,
                HttpContext.RequestAborted);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        // Best-effort MinIO cleanup after the DB commit. A storage failure
        // must not undo the deletion — the row is already gone and the
        // operator expects the agent to disappear from the list.
        foreach (var objectKey in storageKeys)
        {
            try
            {
                await storage.DeleteObjectAsync(objectKey, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Best-effort delete of object {ObjectKey} failed after agent {AgentId} removal", objectKey, agentId);
            }
        }

        return NoContent();
    }

    [Authorize(Policy = AuthConstants.AgentEnrollmentPolicy)]
    [EnableRateLimiting("enrollment-public")]
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
    [EnableRateLimiting("enrollment-public")]
    [HttpPost("register_pending")]
    public async Task<IActionResult> RegisterPending(
        [FromBody] PendingAgentRequest request,
        [FromServices] AgentInstallTokenService installTokenService)
    {
        // Auth handler classifies the presented token as either "install"
        // (a single-use admin-minted token bound to one agent slot) or
        // "shared" (the legacy AgentEnrollment:EnrollmentToken). The
        // install path auto-approves and returns an access token; the
        // shared path stays the existing pending-approval queue.
        var kind = HttpContext.Items[AgentEnrollmentAuthenticationHandler.TokenKindItemKey] as string;
        if (kind == AgentEnrollmentAuthenticationHandler.KindInstall)
        {
            var rawToken = HttpContext.Items[AgentEnrollmentAuthenticationHandler.TokenRawItemKey] as string
                ?? string.Empty;

            var consumed = await installTokenService.TryConsumeAsync(
                rawToken,
                request.MachineName,
                HttpContext.RequestAborted);

            // Lost the race against another caller (or the cleanup
            // service) between auth recognition and consume — fail closed.
            if (consumed is null)
            {
                return Unauthorized();
            }

            var agent = await _agentService.ApproveFromInstallTokenAsync(
                request.MachineName,
                request.OsType,
                request.OsVersion,
                consumed.PreApprovedName,
                consumed.CreatedByUserId,
                consumed.Id);

            var accessToken = _tokenService.CreateAgentToken(agent);
            return Ok(new PendingAgentRegisterResponse(agent.Id, agent.Id, accessToken));
        }

        var pendingId = await _agentService.RegisterPending(request.MachineName, request.OsType, request.OsVersion);
        return Ok(new PendingAgentRegisterResponse(pendingId));
    }

    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    [EnableRateLimiting("install-token-create")]
    [HttpPost("install-tokens")]
    public async Task<IActionResult> CreateInstallToken(
        [FromBody] CreateInstallTokenRequest request,
        [FromServices] AgentInstallTokenService installTokenService,
        [FromServices] IAuditLogRepository auditLogRepository)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Unauthorized();
        }

        var ttl = request.TtlMinutes.HasValue
            ? TimeSpan.FromMinutes(request.TtlMinutes.Value)
            : AgentInstallTokenService.DefaultTtl;

        var generated = await installTokenService.GenerateAsync(
            actorUserId.Value,
            request.PreApprovedName,
            ttl,
            HttpContext.RequestAborted);

        await auditLogRepository.AddAsync(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorId = actorUserId.Value,
            Action = "agent.install_token.created",
            TargetId = generated.Record.Id,
            Details = $"expires_at={generated.Record.ExpiresAt:O} preapproved={generated.Record.PreApprovedName ?? "-"}",
            OccurredAt = DateTime.UtcNow,
        });
        await auditLogRepository.SaveChangesAsync();

        return Ok(new CreateInstallTokenResponse(
            generated.Record.Id,
            generated.Token,
            generated.Record.ExpiresAt));
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
