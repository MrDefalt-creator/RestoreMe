using Backup.Agent.Worker.Interfaces;
using Backup.Agent.Worker.Options;
using Backup.Agent.Worker.State;
using Microsoft.Extensions.Options;

namespace Backup.Agent.Worker.Services;

public sealed class ApiEndpointResolver : IApiEndpointResolver
{
    private readonly ApiOptions _apiOptions;
    private readonly IAgentState _agentState;

    public ApiEndpointResolver(IOptions<ApiOptions> apiOptions, IAgentState agentState)
    {
        _apiOptions = apiOptions.Value;
        _agentState = agentState;
    }

    public async Task<ResolvedApiEndpoint> ResolveAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_apiOptions.BaseUrl))
        {
            return new ResolvedApiEndpoint(Normalize(_apiOptions.BaseUrl), "configuration");
        }

        var storedServerAddress = await _agentState.TryGetServerAddressAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(storedServerAddress))
        {
            return new ResolvedApiEndpoint(Normalize(storedServerAddress), "local state");
        }

        throw new InvalidOperationException(
            "Api:BaseUrl is not configured and no server address was found in local state.");
    }

    private static string Normalize(string baseUrl)
    {
        return baseUrl.Trim().TrimEnd('/') + "/";
    }
}
