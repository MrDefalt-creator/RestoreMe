using System.IO.Compression;
using Backup.Agent.Worker.Interfaces;

namespace Backup.Agent.Worker.Services;

public class RestoreExecuter : IRestoreExecutor
{
    private readonly ILogger<RestoreExecuter> _logger;
    private readonly IRestoreApiClient _restoreApiClient;
    private readonly IMinioStorageClient _storageClient;
    private readonly LogicalRestoreService _logicalRestoreService;
    private readonly IChecksumService _checksumService;

    public RestoreExecuter(
        ILogger<RestoreExecuter> logger,
        IRestoreApiClient restoreApiClient,
        IMinioStorageClient storageClient,
        LogicalRestoreService logicalRestoreService,
        IChecksumService checksumService)
    {
        _logger = logger;
        _restoreApiClient = restoreApiClient;
        _storageClient = storageClient;
        _logicalRestoreService = logicalRestoreService;
        _checksumService = checksumService;
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

            // Integrity gate: verify the downloaded artifact's SHA256 against the
            // expected checksum BEFORE touching the restore target, so a corrupt
            // or truncated download can never overwrite live data.
            var computedChecksum = await _checksumService.ComputeSha256Async(tempFilePath, cancellationToken);
            if (!RestoreChecksumGate.ShouldProceed(pending.Checksum, computedChecksum))
            {
                throw new InvalidOperationException(
                    "Artifact checksum verification failed: downloaded data does not match the expected SHA256. Restore target left untouched.");
            }
            if (string.IsNullOrWhiteSpace(pending.Checksum))
            {
                _logger.LogWarning("Restore job {JobId}: artifact has no recorded checksum; skipping integrity check", pending.JobId);
            }

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
            SnapshotBeforeRestore(parentDir, isDirectory: true);
            Directory.CreateDirectory(parentDir);
            ExtractZipSafely(tempFilePath, parentDir);
        }
        else
        {
            var dir = Path.GetDirectoryName(sourcePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            SnapshotBeforeRestore(sourcePath, isDirectory: false);
            File.Copy(tempFilePath, sourcePath, overwrite: true);
        }
    }

    private void SnapshotBeforeRestore(string targetPath, bool isDirectory)
    {
        // Rename the existing target out of the way before overwriting so a
        // bad/corrupted restore can be rolled back manually. Snapshot suffix
        // includes a UTC timestamp; collisions across same-second restarts
        // get an extra Guid fragment.
        try
        {
            if (isDirectory)
            {
                if (!Directory.Exists(targetPath)) return;
                if (!Directory.EnumerateFileSystemEntries(targetPath).Any()) return;

                var snapshotPath = BuildSnapshotPath(targetPath);
                Directory.Move(targetPath, snapshotPath);
                _logger.LogInformation(
                    "Pre-restore snapshot of directory {Target} created at {Snapshot}",
                    targetPath, snapshotPath);
                return;
            }

            if (!File.Exists(targetPath)) return;

            var fileSnapshotPath = BuildSnapshotPath(targetPath);
            File.Move(targetPath, fileSnapshotPath);
            _logger.LogInformation(
                "Pre-restore snapshot of file {Target} created at {Snapshot}",
                targetPath, fileSnapshotPath);
        }
        catch (Exception ex)
        {
            // Snapshot is best-effort; don't fail the whole restore if the
            // process can't move the file (locked, permissions, cross-volume).
            // Operators still get the restore — they just lose the rollback path.
            _logger.LogWarning(ex,
                "Pre-restore snapshot of {Target} failed; proceeding with overwrite",
                targetPath);
        }
    }

    private static string BuildSnapshotPath(string targetPath)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var candidate = $"{targetPath}.pre-restore-{timestamp}";
        if (!File.Exists(candidate) && !Directory.Exists(candidate))
        {
            return candidate;
        }

        return $"{targetPath}.pre-restore-{timestamp}-{Guid.NewGuid():N}";
    }

    private void ExtractZipSafely(string archivePath, string destinationRoot)
    {
        // Guard against ZIP slip: validate each entry resolves inside destinationRoot
        // before extracting. A malicious or corrupted artifact with entries like
        // "../../etc/passwd" would otherwise let the agent overwrite arbitrary files
        // with whatever privileges its process has.
        var fullDestinationRoot = Path.GetFullPath(destinationRoot);
        var rootWithSeparator = fullDestinationRoot.EndsWith(Path.DirectorySeparatorChar)
            ? fullDestinationRoot
            : fullDestinationRoot + Path.DirectorySeparatorChar;

        using var archive = ZipFile.OpenRead(archivePath);

        foreach (var entry in archive.Entries)
        {
            var targetPath = Path.GetFullPath(Path.Combine(fullDestinationRoot, entry.FullName));

            var isDirectoryEntry = string.IsNullOrEmpty(entry.Name);
            var pathToValidate = isDirectoryEntry
                ? (targetPath.EndsWith(Path.DirectorySeparatorChar) ? targetPath : targetPath + Path.DirectorySeparatorChar)
                : targetPath;

            if (!pathToValidate.StartsWith(rootWithSeparator, StringComparison.Ordinal) &&
                !string.Equals(pathToValidate.TrimEnd(Path.DirectorySeparatorChar), fullDestinationRoot, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing to extract zip entry '{entry.FullName}' — resolved path escapes the restore directory.");
            }

            if (isDirectoryEntry)
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            var entryDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(entryDirectory))
            {
                Directory.CreateDirectory(entryDirectory);
            }

            entry.ExtractToFile(targetPath, overwrite: true);
        }
    }
}
