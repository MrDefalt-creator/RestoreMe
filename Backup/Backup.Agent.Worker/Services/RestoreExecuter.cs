using System.IO.Compression;
using Backup.Agent.Worker.Interfaces;

namespace Backup.Agent.Worker.Services;

public class RestoreExecuter : IRestoreExecutor
{
    private readonly ILogger<RestoreExecuter> _logger;
    private readonly IRestoreApiClient _restoreApiClient;
    private readonly IMinioStorageClient _storageClient;
    private readonly LogicalRestoreService _logicalRestoreService;

    public RestoreExecuter(
        ILogger<RestoreExecuter> logger,
        IRestoreApiClient restoreApiClient,
        IMinioStorageClient storageClient,
        LogicalRestoreService logicalRestoreService)
    {
        _logger = logger;
        _restoreApiClient = restoreApiClient;
        _storageClient = storageClient;
        _logicalRestoreService = logicalRestoreService;
    }

    public async Task ExecutePendingAsync(Guid agentId, CancellationToken cancellationToken)
    {
        var pending = await _restoreApiClient.GetPendingRestoreAsync(agentId, cancellationToken);
        if (pending is null) return;

        var tempFilePath = Path.Combine(Path.GetTempPath(), $"restore_{Guid.NewGuid():N}_{pending.FileName}");

        try
        {
            _logger.LogInformation("Starting restore job {JobId}, artifact {ObjectKey}", pending.JobId, pending.ObjectKey);

            var downloadUrl = await _restoreApiClient.RequestDownloadTicketAsync(pending.JobId, cancellationToken);
            await _storageClient.DownloadFileAsync(downloadUrl, tempFilePath, cancellationToken);

            await ApplyRestoreAsync(pending.PolicyType, pending.SourcePath, pending.DatabaseSettings, tempFilePath, cancellationToken);

            await _restoreApiClient.CompleteRestoreJobAsync(pending.JobId, cancellationToken);
            _logger.LogInformation("Restore job {JobId} completed successfully", pending.JobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Restore job {JobId} failed", pending.JobId);
            try { await _restoreApiClient.FailRestoreJobAsync(pending.JobId, ex.Message, cancellationToken); }
            catch (Exception failEx) { _logger.LogError(failEx, "Failed to report restore failure for job {JobId}", pending.JobId); }
            throw;
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to delete temp restore file {Path}", tempFilePath); }
            }
        }
    }

    private async Task ApplyRestoreAsync(
        string policyType,
        string sourcePath,
        Backup.Shared.Contracts.DTOs.Policies.BackupPolicyDatabaseSettingsDto? dbSettings,
        string tempFilePath,
        CancellationToken cancellationToken)
    {
        if (string.Equals(policyType, "postgres", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(policyType, "mysql", StringComparison.OrdinalIgnoreCase))
        {
            if (dbSettings is null)
                throw new InvalidOperationException("Database settings are required for logical restore.");
            await _logicalRestoreService.RestoreAsync(policyType, dbSettings, tempFilePath, cancellationToken);
            return;
        }

        // filesystem restore
        var isZip = tempFilePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        if (isZip)
        {
            var parentDir = Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrEmpty(parentDir)) parentDir = ".";
            Directory.CreateDirectory(parentDir);
            ZipFile.ExtractToDirectory(tempFilePath, parentDir, overwriteFiles: true);
        }
        else
        {
            var dir = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Copy(tempFilePath, sourcePath, overwrite: true);
        }
    }
}
