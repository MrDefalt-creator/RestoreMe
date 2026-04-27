using Backup.Agent.Worker.DTOs;
using Backup.Agent.Worker.Interfaces;
using Backup.Agent.Worker.State;
using Backup.Shared.Contracts.DTOs.Jobs;
using Backup.Shared.Contracts.DTOs.Policies;

namespace Backup.Agent.Worker.Services;

public class BackupExecuter : IBackupExecutor
{
    
    private readonly ILogger<BackupExecuter> _logger;
    private readonly IAgentState _agentState;
    private readonly IBackupApiClient _backupApiClient;
    private readonly IMinioStorageClient _storageClient;
    private readonly IArchiveService _archiveService;
    private readonly ILogicalBackupService _logicalBackupService;
        
    public BackupExecuter(
        ILogger<BackupExecuter> logger,
        IAgentState agentState,
        IBackupApiClient backupApiClient,
        IArchiveService archiveService,
        IMinioStorageClient storageClient,
        ILogicalBackupService logicalBackupService)
    {
        _logger = logger;
        _agentState = agentState;
        _backupApiClient = backupApiClient;
        _archiveService = archiveService;
        _storageClient = storageClient;
        _logicalBackupService = logicalBackupService;
    }
    
    public async Task ExecutePolicyAsync(BackupPolicyDto policy, CancellationToken cancellationToken)
    {
        Guid? agentId = await _agentState.TryGetAgentIdAsync(cancellationToken);

        if (!agentId.HasValue || agentId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Agent id is not set");
        }
        
        Guid jobId = Guid.Empty;
        PreparedBackupPayload? preparedPayload = null;

        try
        {
            _logger.LogInformation(
                "Staring backup for policy id {PolicyId}. SourcePath: {SourcePath}",
                policy.Id, policy.SourcePath);

            jobId = await _backupApiClient.StartBackupJobAsync(agentId.Value, policy.Id, cancellationToken);

            preparedPayload = await PreparePayloadAsync(policy, cancellationToken);

            var ticket = await _backupApiClient.RequestUploadTicketAsync(
                new RequestUploadTicketRequest(
                    jobId,
                    policy.Id,
                    preparedPayload.FileName,
                    preparedPayload.ContentType),
                cancellationToken);


            var uploadResult = await _storageClient.UploadFileAsync(
                ticket.UploadUrl,
                preparedPayload.FilePath,
                preparedPayload.ContentType,
                cancellationToken);

            await _backupApiClient.AddArtifactAsync(jobId, preparedPayload.FileName, ticket.ObjectKey, uploadResult.Size,
                uploadResult.Checksum, cancellationToken);
            await _backupApiClient.FinishBackupJobAsync(jobId, cancellationToken);

            _logger.LogInformation(
                "Backup completed successfully. PolicyId: {PolicyId}, JobId: {JobId}",
                policy.Id,
                jobId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Backup failed. PolicyId: {PolicyId}, JobId: {JobId}",
                policy.Id,
                jobId);
            
            if (jobId != Guid.Empty)
            {
                try
                {
                    await _backupApiClient.FailBackupJobAsync(
                        jobId,
                        ex.Message,
                        cancellationToken);
                }
                catch (Exception failEx)
                {
                    _logger.LogError(
                        failEx,
                        "Failed to mark backup job as failed. JobId: {JobId}",
                        jobId);
                }
            }
            throw;
        }
        finally
        {
            if (preparedPayload?.ShouldDeleteAfterUpload == true &&
                File.Exists(preparedPayload.FilePath))
            {
                try
                {
                    File.Delete(preparedPayload.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to delete temp backup payload: {PayloadPath}",
                        preparedPayload.FilePath);
                }
            }
        }
    }

    private async Task<PreparedBackupPayload> PreparePayloadAsync(
        BackupPolicyDto policy,
        CancellationToken cancellationToken)
    {
        if (string.Equals(policy.Type, "postgres", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(policy.Type, "mysql", StringComparison.OrdinalIgnoreCase))
        {
            return await _logicalBackupService.CreateDumpAsync(policy, cancellationToken);
        }

        if (File.Exists(policy.SourcePath))
        {
            return new PreparedBackupPayload(
                policy.SourcePath,
                Path.GetFileName(policy.SourcePath),
                "application/octet-stream",
                false);
        }

        if (Directory.Exists(policy.SourcePath))
        {
            var tempArchivePath = await _archiveService.CreateZipFromDirectoryAsync(
                policy.SourcePath,
                cancellationToken);

            return new PreparedBackupPayload(
                tempArchivePath,
                Path.GetFileName(tempArchivePath),
                "application/zip",
                true);
        }

        throw new FileNotFoundException($"Source path {policy.SourcePath} does not exist");
    }

}
