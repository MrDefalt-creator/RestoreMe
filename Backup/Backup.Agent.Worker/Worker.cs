using Backup.Agent.Worker.Options;
using Backup.Agent.Worker.Services;
using Backup.Agent.Worker.State;
using Backup.Shared.Contracts.DTOs;
using Microsoft.Extensions.Options;

namespace Backup.Agent.Worker;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IAgentApiClient _apiClient;
    private readonly AgentOptions _agentOptions;
    private readonly IAgentState _agentState;

    public Worker(ILogger<Worker> logger, 
        IAgentApiClient apiClient, 
        IOptions<AgentOptions> agentOptions, IAgentState agentState)
    {
        _logger = logger;
        _apiClient = apiClient;
        _agentState = agentState;
        _agentOptions = agentOptions.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

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
        
        var agentId = await ResolveAgentId(stoppingToken);
        
        _logger.LogInformation(
            "Backup worker started. AgentId: {AgentId}, MachineName: {MachineName}",
            agentId,
            Environment.MachineName
        );

        var heartbeatInterval = TimeSpan.FromSeconds(_agentOptions.HeartbeatIntervalSeconds);
        var policySyncInterval = TimeSpan.FromSeconds(_agentOptions.PolicySyncIntervalSeconds);
        var nextPolicySyncAtUtc = DateTime.UtcNow;

        using var timer = new PeriodicTimer(heartbeatInterval);

        nextPolicySyncAtUtc = await ProcessIterationAsync(agentId, nextPolicySyncAtUtc, policySyncInterval, stoppingToken);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            nextPolicySyncAtUtc = await ProcessIterationAsync(agentId, nextPolicySyncAtUtc, policySyncInterval, stoppingToken);
        }
    }


    private async Task<Guid> ResolveAgentId(CancellationToken cancellationToken)
    {
        if (_agentOptions.AgentId.HasValue && _agentOptions.AgentId != Guid.Empty)
        {
            _logger.LogInformation("AgentId loaded from configuration. AgentId: {AgentId}", _agentOptions.AgentId);
            
            return _agentOptions.AgentId.Value;
        }
        
        var storedAgentId = await _agentState.TryGetAgentIdAsync(cancellationToken);

        if (storedAgentId.HasValue)
        {
            _logger.LogInformation("AgentId loaded from local state. AgentId: {AgentId}", storedAgentId.Value);
            return storedAgentId.Value;
        }

        _logger.LogInformation("AgentId not found. Starting pending registration flow");

        var pendingId = await _apiClient.RegisterPendingAsync(
            new PendingAgentRequest(Environment.MachineName, GetOsType(), Environment.OSVersion.VersionString), cancellationToken);
        
        _logger.LogInformation("Pending registration succeeded. AgentId: {PendingId}", pendingId);

        while (!cancellationToken.IsCancellationRequested)
        {
            var status = await _apiClient.GetPendingStatusAsync(pendingId, cancellationToken);

            if (status.ApprovedAgentId.HasValue)
            {
                var approvedAgentId = status.ApprovedAgentId.Value;
                
                await _agentState.SaveAgentIdAsync(approvedAgentId, cancellationToken);
                
                _logger.LogInformation("Agent approved. AgentId: {AgentId}", approvedAgentId);
                
                return approvedAgentId;
            }
            
            _logger.LogInformation("Waiting for agent approval...");
            
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
        }
        
        throw new OperationCanceledException("Registration flow was cancelled");
        
    }


    private static string GetOsType()
    {
        if (OperatingSystem.IsWindows()) return "Windows";
        if (OperatingSystem.IsLinux()) return "Linux";
        if (OperatingSystem.IsMacOS()) return "MacOS";
        
        return "Unknown";
    }



    private async Task<DateTime> ProcessIterationAsync(
            Guid agentId,
            DateTime nextPolicySyncAtUtc,
            TimeSpan policySyncInterval,
            CancellationToken cancellationToken
        )
        {
            try
            {
                var heartbeatSent = await _apiClient.SendHeartbeatAsync(
                    agentId,
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

                var policies = await _apiClient.GetPoliciesAsync(agentId, cancellationToken);

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

