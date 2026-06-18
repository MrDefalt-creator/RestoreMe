using Backup.Agent.Worker.Interfaces;
using Backup.Agent.Worker.Options;
using Backup.Agent.Worker.Startup;
using Backup.Agent.Worker.State;
using Microsoft.Extensions.Options;

namespace Backup.Agent.Worker.Services;

public sealed class ApiEndpointResolver : IApiEndpointResolver
{
    private readonly ApiOptions _apiOptions;
    private readonly IAgentState _agentState;
    private readonly AgentStartupOptions _startup;

    public ApiEndpointResolver(
        IOptions<ApiOptions> apiOptions,
        IAgentState agentState,
        AgentStartupOptions startup)
    {
        _apiOptions = apiOptions.Value;
        _agentState = agentState;
        _startup = startup;
    }

    public async Task<ResolvedApiEndpoint> ResolveAsync(CancellationToken cancellationToken)
    {
        // Resolution order:
        //   1. CLI / ENV override — operator explicitly pointed us somewhere
        //   2. Local state — what previous runs persisted
        //   3. Static config (appsettings.json)
        // The override wins so a `--server` flag is enough to redirect the
        // agent — no manual state-file deletion required.
        if (!string.IsNullOrWhiteSpace(_startup.ExplicitServerUrl))
        {
            return new ResolvedApiEndpoint(
                Normalize(_startup.ExplicitServerUrl),
                _startup.ExplicitServerSource ?? "cli/env override");
        }

        var storedServerAddress = await _agentState.TryGetServerAddressAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(storedServerAddress))
        {
            return new ResolvedApiEndpoint(Normalize(storedServerAddress), "local state");
        }

        return ResolveFromConfig();
    }

    public ResolvedApiEndpoint Resolve()
    {
        if (!string.IsNullOrWhiteSpace(_startup.ExplicitServerUrl))
        {
            return new ResolvedApiEndpoint(
                Normalize(_startup.ExplicitServerUrl),
                _startup.ExplicitServerSource ?? "cli/env override");
        }

        var storedServerAddress = _agentState
            .TryGetServerAddressAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

        if (!string.IsNullOrWhiteSpace(storedServerAddress))
        {
            return new ResolvedApiEndpoint(Normalize(storedServerAddress), "local state");
        }

        return ResolveFromConfig();
    }

    private ResolvedApiEndpoint ResolveFromConfig()
    {
        if (!string.IsNullOrWhiteSpace(_apiOptions.BaseUrl))
        {
            return new ResolvedApiEndpoint(Normalize(_apiOptions.BaseUrl), "configuration");
        }

        throw new InvalidOperationException(
            "Api:BaseUrl is not configured and no server address was found in local state. " +
            "Pass --server <url> or set RESTOREME_SERVER env var.");
    }

    private static string Normalize(string baseUrl)
    {
        return baseUrl.Trim().TrimEnd('/') + "/";
    }
}
