using Backup.Server.Application.Services;

namespace Backup.Server.Api.HostedServices;

// Sweeps unused expired AgentInstallToken rows out of the DB. Used rows
// stick around for audit history; the service only deletes ones that
// were never consumed and whose grace window has passed.
public sealed class AgentInstallTokenCleanupService : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AgentInstallTokenCleanupService> _logger;

    public AgentInstallTokenCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<AgentInstallTokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run once at startup so cold starts don't leave stale rows
        // hanging until the first tick.
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
            var service = scope.ServiceProvider.GetRequiredService<AgentInstallTokenService>();
            var removed = await service.CleanupExpiredAsync(cancellationToken);
            if (removed > 0)
            {
                _logger.LogInformation("Pruned {Count} expired unused agent install tokens.", removed);
            }
        }
        catch (OperationCanceledException)
        {
            // Host is shutting down.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up expired agent install tokens.");
        }
    }
}
