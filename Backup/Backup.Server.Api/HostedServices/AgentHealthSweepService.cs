using Backup.Server.Application.Services;

namespace Backup.Server.Api.HostedServices;

// Polls every agent's heartbeat freshness and fires online/offline
// transition notifications. The cadence (30s) is short enough that
// operators learn about an outage within one cycle, long enough that
// the sweep can't compete with normal request traffic on a single-DB
// self-hosted deployment.
public sealed class AgentHealthSweepService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentHealthSweepService> _logger;

    public AgentHealthSweepService(
        IServiceScopeFactory scopeFactory,
        ILogger<AgentHealthSweepService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The first tick happens on startup so the baseline pass (which
        // doesn't notify) is done before any operator request arrives.
        await TickAsync(stoppingToken);

        using var timer = new PeriodicTimer(TickInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AgentHealthService>();
            var transitions = await service.SweepAsync(cancellationToken);
            if (transitions > 0)
            {
                _logger.LogInformation("Agent health sweep: {Transitions} transition(s) notified.", transitions);
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent health sweep failed.");
        }
    }
}
