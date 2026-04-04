using Backup.Agent.Worker.Options;
using Backup.Agent.Worker.Services;
using Microsoft.Extensions.Options;

namespace Backup.Agent.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IAgentApiClient _apiClient;
    private readonly AgentOptions _agentOptions;

    public Worker(ILogger<Worker> logger, 
        IAgentApiClient apiClient, 
        IOptions<AgentOptions> agentOptions)
    {
        _logger = logger;
        _apiClient = apiClient;
        _agentOptions = agentOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_agentOptions.AgentId == Guid.Empty)
        {
            _logger.LogError("AgentId is empty. Worker stopped");
            return;
        }

        if (_agentOptions.HeartbeatIntervalSeconds <= 0)
        {
            _logger.LogError("HeartbeatIntervalSeconds must be greater then zero. Worker stopped");
            return;
        }

        if (_agentOptions.PolicySyncIntervalSeconds <= 0)
        {
            _logger.LogError("PolicySyncIntervalSeconds must be grater then zero. Worker stopped");
            return;
        }

        _logger.LogInformation(
            "Backup worker started. AgentId: {AgentId}, MachineName: {MachineName}",
            _agentOptions.AgentId,
            Environment.MachineName
        );

        var heartbeatInterval = TimeSpan.FromSeconds(_agentOptions.HeartbeatIntervalSeconds);
        var policySyncInterval = TimeSpan.FromSeconds(_agentOptions.PolicySyncIntervalSeconds);
        var nextPolicySyncAtUtc = DateTime.UtcNow;

        using var timer = new PeriodicTimer(heartbeatInterval);

        nextPolicySyncAtUtc = await ProcessIterationAsync(nextPolicySyncAtUtc, policySyncInterval, stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            nextPolicySyncAtUtc = await ProcessIterationAsync(nextPolicySyncAtUtc, policySyncInterval, stoppingToken);
        }
    }



    private async Task<DateTime> ProcessIterationAsync(
            DateTime nextPolicySyncAtUtc,
            TimeSpan policySyncInterval,
            CancellationToken cancellationToken
        )
        {
            try
            {
                var heartbeatSent = await _apiClient.SendHeartbeatAsync(
                    _agentOptions.AgentId,
                    cancellationToken);

                if (!heartbeatSent)
                {
                    _logger.LogWarning("Heartbeat was not accepted by server.");
                    return nextPolicySyncAtUtc;
                }

                _logger.LogInformation("Heartbeat sent successfully.");

                if (DateTime.UtcNow < nextPolicySyncAtUtc)
                {
                    return nextPolicySyncAtUtc;
                }

                var policies = await _apiClient.GetPoliciesAsync(_agentOptions.AgentId, cancellationToken);

                _logger.LogInformation("Policies synchronized. Count: {PoliciesCount}", policies.Count);

                foreach (var policy in policies)
                {
                    _logger.LogInformation(
                        "Policy: {PolicyId} | {PolicyName} | {PolicySourcePath}",
                        policy.Id,
                        policy.Name,
                        policy.SourcePath
                    );
                }

                return DateTime.UtcNow.Add(policySyncInterval);

            }
            catch (OperationCanceledException)
            {
                return nextPolicySyncAtUtc;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error in worker iteration.");
                return nextPolicySyncAtUtc;
            }
        }
}

