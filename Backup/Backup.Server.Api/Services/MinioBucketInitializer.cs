using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;

namespace Backup.Server.Api.Services;

// Creates the configured MinIO bucket once at startup so per-request
// upload-ticket calls do not have to round-trip BucketExistsAsync.
// Soft-fails: if MinIO is unreachable, logs a warning and lets the
// storage service handle the first call (which will retry the check).
public sealed class MinioBucketInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly BucketReadyState _state;
    private readonly ILogger<MinioBucketInitializer> _logger;

    public MinioBucketInitializer(
        IServiceScopeFactory scopeFactory,
        BucketReadyState state,
        ILogger<MinioBucketInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var storage = scope.ServiceProvider.GetRequiredService<IStorageAccessService>();
            await storage.EnsureBucketExistsAsync(cancellationToken);
            _state.MarkReady();
            _logger.LogInformation("MinIO bucket is ready.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to verify MinIO bucket at startup; will retry on first request.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
