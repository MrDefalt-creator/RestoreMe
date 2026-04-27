using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs;
using Backup.Shared.Contracts.DTOs.Policies;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Server.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PoliciesController : ControllerBase
{
    private readonly PoliciesService _policiesService;
    
    public  PoliciesController(PoliciesService policiesService)
    {
        _policiesService = policiesService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPolicies()
    {
        var policies = await _policiesService.GetAllPolicies();
        return Ok(policies.Select(MapPolicy));
    }

    [HttpGet("{policyId:guid}")]
    public async Task<IActionResult> GetPolicyByIdRoute([FromRoute] Guid policyId)
    {
        var policy = await _policiesService.GetPolicyById(policyId);
        return Ok(MapPolicy(policy));
    }
    
    [HttpGet("agent/{agentId:guid}")]
    public async Task<IActionResult> GetPoliciesByAgent([FromRoute] Guid agentId)
    {
        var policies = await _policiesService.GetAllPolicies(agentId);
        return Ok(policies.Select(MapPolicy));
    }

    [HttpPost("create_policy/{agentId}")]
    public async Task<IActionResult> CreatePolicyForAgent([FromRoute] Guid agentId, [FromBody] CreateBackupPolicyRequest request)
    {
        try
        {
            var policy = await _policiesService.CreatePolicy(
                agentId,
                request.Type,
                request.Name,
                request.SourcePath,
                request.Interval,
                request.DatabaseSettings);

            var response = new CreatePolicyResponse(policy.Id, policy.Name, policy.AgentId);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("get_policies/{agentId}")]
    public async Task<IActionResult> GetPolicyForAgent([FromRoute] Guid agentId)
    {
        var policies = await _policiesService.GetAllPolicies(agentId);
        return Ok(policies.Select(policy => new BackupPolicyDto(
            policy.Id,
            MapPolicyType(policy.Type),
            policy.Name,
            policy.SourcePath,
            policy.IsEnabled,
            policy.NextRunAt,
            MapDatabaseSettings(policy.DatabaseSettings))));
    }

    [HttpGet("get_policy/{policyId}")]
    public async Task<IActionResult> GetPolicy([FromRoute] Guid policyId)
    {
        var policy = await _policiesService.GetPolicyById(policyId);
        
        var response = new BackupPolicyDto(
            policy.Id,
            MapPolicyType(policy.Type),
            policy.Name,
            policy.SourcePath,
            policy.IsEnabled,
            policy.NextRunAt,
            MapDatabaseSettings(policy.DatabaseSettings));
        
        return Ok(response);
    }

    [HttpPut("{policyId:guid}")]
    public async Task<IActionResult> UpdatePolicy([FromRoute] Guid policyId, [FromBody] UpdateBackupPolicyRequest request)
    {
        var policy = await _policiesService.UpdatePolicy(
            policyId,
            request.AgentId,
            request.Type,
            request.Name,
            request.SourcePath,
            request.IntervalSeconds,
            request.IsEnabled,
            request.DatabaseSettings);

        return Ok(MapPolicy(policy));
    }

    [HttpPatch("{policyId:guid}/toggle")]
    public async Task<IActionResult> TogglePolicy([FromRoute] Guid policyId)
    {
        var policy = await _policiesService.TogglePolicy(policyId);
        return Ok(MapPolicy(policy));
    }

    [HttpPost("mark_policy_executed/{policyId}")]
    public async Task<IActionResult> MarkPolicyExecuted([FromRoute] Guid policyId)
    {
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
            MapDatabaseSettings(policy.DatabaseSettings));
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

    private static BackupPolicyDatabaseSettingsDto? MapDatabaseSettings(BackupPolicyDatabaseSettings? settings)
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
            settings.Password);
    }
}
