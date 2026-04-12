using Backup.Server.Application.Services;
using Backup.Shared.Contracts.DTOs;
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

    [HttpPost("create_policy/{agentId}")]
    public async Task<IActionResult> CreatePolicyForAgent([FromRoute] Guid agentId, [FromBody] CreateBackupPolicyRequest request)
    {
        var policy = await _policiesService.CreatePolicy(agentId, request.Name, request.SourcePath);
        
        var response = new CreatePolicyResponse(policy.Id, policy.Name, policy.AgentId);
        return Ok(response);
    }

    [HttpGet("get_policies/{agentId}")]
    public async Task<IActionResult> GetPolicyForAgent([FromRoute] Guid agentId)
    {
        var policies = await _policiesService.GetAllPolicies(agentId);
        return Ok(policies);
    }

    [HttpGet("get_policy/{policyId}")]
    public async Task<IActionResult> GetPolicy([FromRoute] Guid policyId)
    {
        var policy = await _policiesService.GetPolicyById(policyId);
        
        var response = new BackupPolicyDto(policy.Id, policy.Name, policy.SourcePath, policy.IsEnabled);
        
        return Ok(response);
    }
    
    
}