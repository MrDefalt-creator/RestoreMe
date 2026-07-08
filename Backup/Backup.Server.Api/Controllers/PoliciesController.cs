using Backup.Server.Api.Security;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    private readonly PoliciesService _policiesService;

    public PoliciesController(PoliciesService policiesService)
    {
        _policiesService = policiesService;
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet]
    public async Task<IActionResult> GetPolicies()
    {
        var policies = await _policiesService.GetAllPolicies();
        return Ok(policies.Select(MapPolicy));
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("{policyId:guid}")]
    public async Task<IActionResult> GetPolicyByIdRoute([FromRoute] Guid policyId)
    {
        var policy = await _policiesService.GetPolicyById(policyId);
        return Ok(MapPolicy(policy));
    }

    [Authorize(Policy = AuthConstants.AdminReadPolicy)]
    [HttpGet("agent/{agentId:guid}")]
    public async Task<IActionResult> GetPoliciesByAgent([FromRoute] Guid agentId)
    {
        var policies = await _policiesService.GetAllPolicies(agentId);
        return Ok(policies.Select(MapPolicy));
    }

    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    [HttpPost("create_policy/{agentId:guid}")]
    public async Task<IActionResult> CreatePolicyForAgent([FromRoute] Guid agentId, [FromBody] CreateBackupPolicyRequest request)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Forbid();
        }

        try
        {
            var policy = await _policiesService.CreatePolicy(
                agentId,
                request.Type,
                request.Name,
                request.SourcePath,
                new PolicyScheduleInput(
                    request.ScheduleKind,
                    request.Interval,
                    request.CronExpression,
                    request.TimeZoneId,
                    request.WindowStartMinutes,
                    request.WindowEndMinutes),
                request.DatabaseSettings,
                request.RetentionDays,
                request.RetentionMaxCount,
                request.RetentionMaxTotalBytes,
                actorUserId.Value);

            var response = new CreatePolicyResponse(policy.Id, policy.Name, policy.AgentId);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [HttpGet("get_policies/{agentId:guid}")]
    public async Task<IActionResult> GetPolicyForAgent([FromRoute] Guid agentId)
    {
        if (User.TryGetAgentId() != agentId)
        {
            return Forbid();
        }

        var policies = await _policiesService.GetAllPolicies(agentId);
        return Ok(policies.Select(MapAgentPolicy));
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [HttpGet("get_policy/{policyId:guid}")]
    public async Task<IActionResult> GetPolicy([FromRoute] Guid policyId)
    {
        var currentAgentId = User.TryGetAgentId();
        if (!currentAgentId.HasValue)
        {
            return Forbid();
        }

        var policy = await _policiesService.GetPolicyById(policyId);
        if (policy.AgentId != currentAgentId.Value)
        {
            return Forbid();
        }

        return Ok(MapAgentPolicy(policy));
    }

    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    [HttpPut("{policyId:guid}")]
    public async Task<IActionResult> UpdatePolicy([FromRoute] Guid policyId, [FromBody] UpdateBackupPolicyRequest request)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Forbid();
        }

        BackupPolicy policy;
        try
        {
            policy = await _policiesService.UpdatePolicy(
                policyId,
                request.AgentId,
                request.Type,
                request.Name,
                request.SourcePath,
                new PolicyScheduleInput(
                    request.ScheduleKind,
                    request.IntervalSeconds,
                    request.CronExpression,
                    request.TimeZoneId,
                    request.WindowStartMinutes,
                    request.WindowEndMinutes),
                request.IsEnabled,
                request.DatabaseSettings,
                request.RetentionDays,
                request.RetentionMaxCount,
                request.RetentionMaxTotalBytes,
                actorUserId.Value);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }

        return Ok(MapPolicy(policy));
    }

    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    [HttpPatch("{policyId:guid}/toggle")]
    public async Task<IActionResult> TogglePolicy([FromRoute] Guid policyId)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Forbid();
        }

        var policy = await _policiesService.TogglePolicy(policyId, actorUserId.Value);
        return Ok(MapPolicy(policy));
    }

    [Authorize(Policy = AuthConstants.AdminWritePolicy)]
    [HttpDelete("{policyId:guid}")]
    public async Task<IActionResult> DeletePolicy([FromRoute] Guid policyId)
    {
        var actorUserId = User.TryGetUserId();
        if (!actorUserId.HasValue)
        {
            return Forbid();
        }

        try
        {
            await _policiesService.DeletePolicy(policyId, actorUserId.Value);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }

        return NoContent();
    }

    [Authorize(Policy = AuthConstants.AgentPolicy)]
    [HttpPost("mark_policy_executed/{policyId:guid}")]
    public async Task<IActionResult> MarkPolicyExecuted([FromRoute] Guid policyId)
    {
        var currentAgentId = User.TryGetAgentId();
        if (!currentAgentId.HasValue)
        {
            return Forbid();
        }

        var policy = await _policiesService.GetPolicyById(policyId);
        if (policy.AgentId != currentAgentId.Value)
        {
            return Forbid();
        }

        await _policiesService.MarkPolicyExecuted(policyId);
        return Ok();
    }

    private static AdminBackupPolicyDto MapPolicy(BackupPolicy policy)
    {
        return new AdminBackupPolicyDto(
            policy.Id,
            policy.AgentId,
            MapPolicyType(policy.Type),
            policy.Name,
            policy.SourcePath,
            policy.IsEnabled,
            policy.IntervalSeconds,
            policy.CreatedAt,
            policy.NextRunAt,
            policy.LastRunAt,
            MapDatabaseSettings(policy.DatabaseSettings, includePassword: false),
            policy.RetentionDays,
            policy.RetentionMaxCount,
            policy.RetentionMaxTotalBytes,
            policy.ConsecutiveFailureCount,
            policy.LastFailureReason,
            policy.AutoDisabledAt,
            policy.ScheduleKind == Domain.Enums.ScheduleKind.Cron ? "cron" : "interval",
            policy.CronExpression,
            policy.TimeZoneId,
            policy.WindowStartMinutes,
            policy.WindowEndMinutes);
    }

    private static BackupPolicyDto MapAgentPolicy(BackupPolicy policy)
    {
        return new BackupPolicyDto(
            policy.Id,
            MapPolicyType(policy.Type),
            policy.Name,
            policy.SourcePath,
            policy.IsEnabled,
            policy.NextRunAt,
            MapDatabaseSettings(policy.DatabaseSettings, includePassword: true));
    }

    private static string MapPolicyType(BackupPolicyType type)
    {
        return type switch
        {
            BackupPolicyType.FileSystem => "filesystem",
            BackupPolicyType.PostgreSqlDump => "postgres",
            BackupPolicyType.MySqlDump => "mysql",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private static BackupPolicyDatabaseSettingsDto? MapDatabaseSettings(
        BackupPolicyDatabaseSettings? settings,
        bool includePassword)
    {
        if (settings == null)
        {
            return null;
        }

        return new BackupPolicyDatabaseSettingsDto(
            settings.Engine switch
            {
                DatabaseEngine.PostgreSql => "postgres",
                DatabaseEngine.MySql => "mysql",
                _ => throw new ArgumentOutOfRangeException(nameof(settings.Engine), settings.Engine, null)
            },
            settings.AuthMode switch
            {
                DatabaseDumpAuthMode.Integrated => "integrated",
                DatabaseDumpAuthMode.Credentials => "credentials",
                _ => throw new ArgumentOutOfRangeException(nameof(settings.AuthMode), settings.AuthMode, null)
            },
            settings.Host,
            settings.Port,
            settings.DatabaseName,
            settings.Username,
            includePassword ? settings.Password : null);
    }
}
