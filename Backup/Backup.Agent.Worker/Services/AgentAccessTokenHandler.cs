using System.Net;
using System.Net.Http.Headers;
using Backup.Agent.Worker.State;
using Microsoft.Extensions.Logging;

namespace Backup.Agent.Worker.Services;

public class AgentAccessTokenHandler : DelegatingHandler
{
    private readonly IAgentState _agentState;
    private readonly ILogger<AgentAccessTokenHandler> _logger;

    public AgentAccessTokenHandler(IAgentState agentState, ILogger<AgentAccessTokenHandler> logger)
    {
        _agentState = agentState;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Headers.Authorization == null)
        {
            var accessToken = await _agentState.TryGetAccessTokenAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            }
        }

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized && request.Headers.Authorization != null)
        {
            _logger.LogError(
                "Backend rejected the agent token at {Method} {Url}. The agent was likely revoked, the JWT key was rotated, " +
                "or the token version drifted. Re-enroll the agent with: BackupAgent --server <url> --enrollment-token <token> --reset-state",
                request.Method,
                request.RequestUri);
        }

        return response;
    }
}
