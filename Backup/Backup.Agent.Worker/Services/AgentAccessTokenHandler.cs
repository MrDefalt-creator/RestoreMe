using System.Net.Http.Headers;
using Backup.Agent.Worker.State;

namespace Backup.Agent.Worker.Services;

public class AgentAccessTokenHandler : DelegatingHandler
{
    private readonly IAgentState _agentState;

    public AgentAccessTokenHandler(IAgentState agentState)
    {
        _agentState = agentState;
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

        return await base.SendAsync(request, cancellationToken);
    }
}
