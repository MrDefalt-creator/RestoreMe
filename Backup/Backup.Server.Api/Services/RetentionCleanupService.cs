using Backup.Server.Application.Interfaces;

namespace Backup.Server.Api.Services;

public class RetentionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionCleanupService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    public RetentionCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<RetentionCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IBackupArtifactRepository>();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageAccessService>();

            var expired = await artifactRepo.GetExpiredArtifactsAsync(cancellationToken);
            if (expired.Count == 0) return;

            _logger.LogInformation("Retention cleanup: found {Count} expired artifact(s)", expired.Count);

            foreach (var artifact in expired)
            {
                try
                {
                    await storage.DeleteObjectAsync(artifact.ObjectKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete object {ObjectKey} from storage", artifact.ObjectKey);
                }

                await artifactRepo.DeleteArtifactAsync(artifact.Id, cancellationToken);
                _logger.LogInformation("Deleted expired artifact {ArtifactId} (policy retention: {Days}d)",
                    artifact.Id, artifact.Job.Policy.RetentionDays);
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retention cleanup failed");
        }
    }
}
