using System.Net.Http.Json;
using Backup.Agent.Worker.Interfaces;
using Backup.Agent.Worker.Options;
using Backup.Shared.Contracts.DTOs.Agents;
using Backup.Shared.Contracts.DTOs.Policies;
using Microsoft.Extensions.Options;

namespace Backup.Agent.Worker.ApiClients;

public class AgentApiClient : IAgentApiClient
{
    private readonly ILogger<AgentApiClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly ApiOptions _apiOptions;
    
    public AgentApiClient(ILogger<AgentApiClient> logger, HttpClient httpClient, IOptions<ApiOptions> apiOptions)
    {
        _logger = logger;
        _httpClient = httpClient;
        _apiOptions = apiOptions.Value;
    }

    public async Task<Guid> RegisterPendingAsync(PendingAgentRequest request, CancellationToken cancellationToken)
    {
        using var httpRequest = CreateEnrollmentRequest(HttpMethod.Post, "/api/Agents/register_pending");
        httpRequest.Content = JsonContent.Create(request);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

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

    public async Task<string> IssueAccessTokenAsync(Guid agentId, string machineName, CancellationToken cancellationToken)
    {
        using var request = CreateEnrollmentRequest(HttpMethod.Post, $"/api/Agents/issue_access_token/{agentId}");
        request.Content = JsonContent.Create(new IssueAgentAccessTokenRequest(machineName));

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<IssueAgentAccessTokenResponse>(cancellationToken);
        if (result == null || string.IsNullOrWhiteSpace(result.AccessToken))
        {
            throw new Exception("IssueAccessToken response is empty.");
        }

        return result.AccessToken;
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
        using var request = CreateEnrollmentRequest(HttpMethod.Get, $"api/Agents/status/{pendingId}");
        var response = await _httpClient.SendAsync(request, cancellationToken);
        
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

    private HttpRequestMessage CreateEnrollmentRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, relativeUrl);

        if (!string.IsNullOrWhiteSpace(_apiOptions.EnrollmentToken))
        {
            request.Headers.Add("X-Agent-Enrollment-Token", _apiOptions.EnrollmentToken);
        }

        return request;
    }
}
