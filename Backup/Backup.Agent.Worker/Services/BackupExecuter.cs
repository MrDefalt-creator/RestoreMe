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
        
    public BackupExecuter(ILogger<BackupExecuter> logger, IAgentState agentState, IBackupApiClient backupApiClient, IArchiveService archiveService, IMinioStorageClient storageClient)
    {
        _logger = logger;
        _agentState = agentState;
        _backupApiClient = backupApiClient;
        _archiveService = archiveService;
        _storageClient = storageClient;
    }
    
    public async Task ExecutePolicyAsync(BackupPolicyDto policy, CancellationToken cancellationToken)
    {
        Guid? agentId = await _agentState.TryGetAgentIdAsync(cancellationToken);

        if (!agentId.HasValue || agentId.Value == Guid.Empty)
        {
            throw new InvalidOperationException("Agent id is not set");
        }
        
        Guid jobId = Guid.Empty;
        string? tempArchivePath = null;

        try
        {
            _logger.LogInformation(
                "Staring backup for policy id {PolicyId}. SourcePath: {SourcePath}",
                policy.Id, policy.SourcePath);

            jobId = await _backupApiClient.StartBackupJobAsync(agentId.Value, policy.Id, cancellationToken);

            string uploadPath;
            string fileName;
            string contentType;

            if (File.Exists(policy.SourcePath))
            {
                uploadPath = policy.SourcePath;
                fileName = Path.GetFileName(policy.SourcePath);
                contentType = "application/octet-stream";
            }
            else if (Directory.Exists(policy.SourcePath))
            {
                tempArchivePath = await _archiveService.CreateZipFromDirectoryAsync(
                    policy.SourcePath,
                    cancellationToken);

                uploadPath = tempArchivePath;
                fileName = Path.GetFileName(tempArchivePath);
                contentType = "application/zip";
            }
            else
            {
                throw new FileNotFoundException($"Source path {policy.SourcePath} does not exist");
            }

            var ticket = await _backupApiClient.RequestUploadTicketAsync(
                new RequestUploadTicketRequest(
                    jobId,
                    policy.Id,
                    fileName,
                    contentType),
                cancellationToken);


            var uploadResult = await _storageClient.UploadFileAsync(
                ticket.UploadUrl,
                uploadPath,
                contentType,
                cancellationToken);

            await _backupApiClient.AddArtifactAsync(jobId, fileName, ticket.ObjectKey, uploadResult.Size,
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
            if (!string.IsNullOrWhiteSpace(tempArchivePath) && File.Exists(tempArchivePath))
            {
                try
                {
                    File.Delete(tempArchivePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to delete temp archive: {ArchivePath}",
                        tempArchivePath);
                }
            }
        }
    }
    

}