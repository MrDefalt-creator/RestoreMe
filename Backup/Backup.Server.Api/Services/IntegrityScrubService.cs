using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Domain.Options;
using Backup.Server.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace Backup.Server.Api.Services;

public class IntegrityScrubService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IntegrityScrubService> _logger;
    private readonly IntegrityOptions _integrityOptions;
    private readonly StorageOptions _storageOptions;
    private readonly TimeSpan _interval;

    public IntegrityScrubService(
        IServiceScopeFactory scopeFactory,
        ILogger<IntegrityScrubService> logger,
        IOptions<IntegrityOptions> integrityOptions,
        IOptions<StorageOptions> storageOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _integrityOptions = integrityOptions.Value;
        _storageOptions = storageOptions.Value;
        var seconds = _integrityOptions.CheckIntervalSeconds;
        _interval = TimeSpan.FromSeconds(seconds > 0 ? seconds : 60);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) { /* shutdown */ }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Integrity scrub sweep failed");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    // Wakes on CheckIntervalSeconds; runs a scrub only when the admin-configured
    // DB schedule says it is due, then advances NextRunAt.
    internal async Task TickAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IIntegrityScrubSettingsRepository>();
        var settings = await settingsRepo.GetOrCreateAsync(cancellationToken);

        if (!settings.IsEnabled)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now < settings.NextRunAt)
        {
            return;
        }

        await RunScrubAsync(settings.BatchSize, cancellationToken);

        settings.LastRunAt = now;
        settings.NextRunAt = IntegrityScheduleCalculator.ComputeNextRun(now, settings.IntervalDays, settings.RunAtMinutesUtc);
        await settingsRepo.UpdateAsync(settings, cancellationToken);
    }

    internal async Task<int> RunScrubAsync(int batchSize, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var artifactRepo = scope.ServiceProvider.GetRequiredService<IBackupArtifactRepository>();
        var storage = scope.ServiceProvider.GetRequiredService<IStorageAccessService>();
        var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var batch = await artifactRepo.GetArtifactsForScrubAsync(batchSize, cancellationToken);
        if (batch.Count == 0)
        {
            return 0;
        }

        var failures = 0;
        foreach (var artifact in batch)
        {
            string? computed = null;
            string? error = null;
            try
            {
                if (_storageOptions.ChecksumVerifyMaxBytes is null
                    || artifact.SizeBytes <= _storageOptions.ChecksumVerifyMaxBytes.Value)
                {
                    computed = await storage.ComputeObjectSha256Async(artifact.ObjectKey, cancellationToken);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                error = ex.Message;
            }

            var outcome = error is not null
                ? ScrubOutcome.Failed
                : IntegrityScrubDecision.Evaluate(
                    artifact.SizeBytes,
                    _storageOptions.ChecksumVerifyMaxBytes,
                    artifact.Checksum,
                    computed);

            switch (outcome)
            {
                case ScrubOutcome.Skipped:
                    await auditRepo.AddAsync(SystemAudit("artifact.scrub_skipped", artifact.Id,
                        $"objectKey={artifact.ObjectKey} size={artifact.SizeBytes} limit={_storageOptions.ChecksumVerifyMaxBytes}"));
                    break;

                case ScrubOutcome.Verified:
                    await artifactRepo.UpdateIntegrityAsync(artifact.Id, ArtifactIntegrityStatus.Verified, DateTime.UtcNow, cancellationToken);
                    break;

                case ScrubOutcome.Failed:
                    failures++;
                    await artifactRepo.UpdateIntegrityAsync(artifact.Id, ArtifactIntegrityStatus.Failed, null, cancellationToken);
                    await auditRepo.AddAsync(SystemAudit("artifact.scrub_failed", artifact.Id,
                        error is not null
                            ? $"objectKey={artifact.ObjectKey} error={error}"
                            : $"objectKey={artifact.ObjectKey} expected={artifact.Checksum} actual={computed}"));
                    break;
            }
        }

        await auditRepo.SaveChangesAsync();

        if (failures > 0)
        {
            await notifications.NotifyIntegrityCheckFailedAsync(failures, cancellationToken);
        }

        _logger.LogInformation("Integrity scrub: checked {Count} artifact(s), {Failures} failure(s)", batch.Count, failures);
        return failures;
    }

    private static AuditLog SystemAudit(string action, Guid targetId, string details) => new()
    {
        Id = Guid.NewGuid(),
        ActorId = null,
        Action = action,
        TargetId = targetId,
        Details = details,
        OccurredAt = DateTime.UtcNow,
    };
}
