using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Options;
using Microsoft.Extensions.Options;

namespace Backup.Server.Api.Services;

public class RetentionCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RetentionCleanupService> _logger;
    private readonly TimeSpan _interval;

    public RetentionCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<RetentionCleanupService> logger,
        IOptions<RetentionOptions> retentionOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var hours = retentionOptions.Value.CleanupIntervalHours;
        _interval = TimeSpan.FromHours(hours > 0 ? hours : 24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupAsync(stoppingToken);
            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var artifactRepo = scope.ServiceProvider.GetRequiredService<IBackupArtifactRepository>();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageAccessService>();
            var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

            var candidates = await artifactRepo.GetArtifactsForRetentionAsync(cancellationToken);
            var deletions = RetentionEvaluator.SelectForDeletion(candidates, DateTime.UtcNow);
            if (deletions.Count == 0)
            {
                return;
            }

            _logger.LogInformation("Retention cleanup: {Count} artifact(s) selected for pruning", deletions.Count);

            var deletedCount = 0;
            long bytesFreed = 0;

            foreach (var deletion in deletions)
            {
                var artifact = deletion.Artifact;
                try
                {
                    await storage.DeleteObjectAsync(artifact.ObjectKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete object {ObjectKey} from storage", artifact.ObjectKey);
                }

                await artifactRepo.DeleteArtifactAsync(artifact.Id, cancellationToken);

                await auditRepo.AddAsync(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    ActorId = null, // system action
                    Action = "retention.deleted",
                    TargetId = artifact.Id,
                    Details = $"policy={artifact.Job?.Policy?.Id} reason={deletion.Reason} bytes={artifact.SizeBytes}",
                    OccurredAt = DateTime.UtcNow,
                });

                deletedCount++;
                bytesFreed += artifact.SizeBytes;
                _logger.LogInformation(
                    "Pruned artifact {ArtifactId} (reason {Reason}, {Bytes} bytes)",
                    artifact.Id, deletion.Reason, artifact.SizeBytes);
            }

            await auditRepo.SaveChangesAsync();

            await notifications.NotifyRetentionCleanedAsync(deletedCount, bytesFreed, cancellationToken);
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
