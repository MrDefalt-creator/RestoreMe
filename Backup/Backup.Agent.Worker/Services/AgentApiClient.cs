using System.Net.Http.Json;
using Backup.Shared.Contracts.DTOs;

namespace Backup.Agent.Worker.Services;

public class AgentApiClient : IAgentApiClient
{
    private readonly ILogger<AgentApiClient> _logger;
    private readonly HttpClient _httpClient;
    
    public AgentApiClient(ILogger<AgentApiClient> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<Guid> RegisterPendingAsync(PendingAgentRequest request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "/api/Agents/register-pending",
            request,
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"RegisterPending request failed with status code {response.StatusCode}");
        }
        
        var result =  await response.Content.ReadFromJsonAsync<PendingAgentRegisterResponse>(cancellationToken);

        if (result == null)
        {
            throw new Exception("RegisterPending request is Empty");
        }

        return result.PendingId;
    }

    public async Task<bool> SendHeartbeatAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsync(
            $"api/Agents/heartbeat/{agentId}",
            content: null
            ,cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Heartbeat request failed for {AgentId} with status code {StatusCode}",
                agentId,
                response.StatusCode
            );
            
            return false;
        }
        return true;
    }
    
    public async Task<PendingAgentStatusResponse> GetPendingStatusAsync(Guid pendingId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync($"api/Agents/status/{pendingId}", cancellationToken);
        
        response.EnsureSuccessStatusCode();
        
        var result = await response.Content.ReadFromJsonAsync<PendingAgentStatusResponse>(cancellationToken);

        if (result == null)
        {
            throw new Exception("GetPending request is Empty");
        }
        
        return result;
    }

    public async Task<IReadOnlyCollection<BackupPolicyDto>> GetPoliciesAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(
            $"api/Policies/get_policies/{agentId}",
            cancellationToken
        );

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "GetPolicies failed for {agentId} with status code {StatusCode}",
                agentId,
                response.StatusCode
                );
            
            return Array.Empty<BackupPolicyDto>();
        }
        
        var polices = await response.Content.ReadFromJsonAsync<List<BackupPolicyDto>>(cancellationToken: cancellationToken);
        
        return polices ?? new List<BackupPolicyDto>();
    }
}