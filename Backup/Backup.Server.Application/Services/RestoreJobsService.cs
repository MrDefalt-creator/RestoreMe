using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs.Policies;
using Backup.Shared.Contracts.DTOs.Restore;

namespace Backup.Server.Application.Services;

public class RestoreJobsService
{
    private readonly IRestoreJobRepository _restoreJobRepository;
    private readonly IBackupArtifactRepository _artifactRepository;
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly IStorageAccessService _storageAccessService;
    private readonly INotificationService _notificationService;

    public RestoreJobsService(
        IRestoreJobRepository restoreJobRepository,
        IBackupArtifactRepository artifactRepository,
        IBackupJobRepository backupJobRepository,
        IStorageAccessService storageAccessService,
        INotificationService notificationService)
    {
        _restoreJobRepository = restoreJobRepository;
        _artifactRepository = artifactRepository;
        _backupJobRepository = backupJobRepository;
        _storageAccessService = storageAccessService;
        _notificationService = notificationService;
    }

    public async Task<Guid> CreateRestoreAsync(CreateRestoreRequest request, CancellationToken cancellationToken = default)
    {
        var artifact = await _artifactRepository.GetArtifactByIdAsync(request.ArtifactId)
            ?? throw new KeyNotFoundException($"Artifact {request.ArtifactId} not found.");

        var backupJob = await _backupJobRepository.GetBackupJob(artifact.JobId)
            ?? throw new KeyNotFoundException($"Backup job {artifact.JobId} not found.");

        var executingAgentId = request.TargetAgentId
            ?? backupJob.AgentId
            ?? throw new InvalidOperationException("Backup job has no owning agent; restore needs an explicit target agent.");

        var job = new RestoreJob
        {
            Id = Guid.NewGuid(),
            ArtifactId = request.ArtifactId,
            AgentId = executingAgentId,
            TargetAgentId = request.TargetAgentId,
            TargetName = request.TargetName,
            DryRun = request.DryRun,
            Force = request.Force,
            Status = RestoreJobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _restoreJobRepository.AddAsync(job);
        await _restoreJobRepository.SaveChangesAsync();

        return job.Id;
    }

    public async Task<RestoreStatusResponse> GetStatusAsync(Guid restoreJobId, CancellationToken cancellationToken = default)
    {
        var job = await _restoreJobRepository.GetByIdAsync(restoreJobId)
            ?? throw new KeyNotFoundException($"Restore job {restoreJobId} not found.");

        var statusStr = job.Status switch
        {
            RestoreJobStatus.Pending => "pending",
            RestoreJobStatus.Running => "running",
            RestoreJobStatus.Completed => "completed",
            RestoreJobStatus.Failed => "failed",
            _ => "pending"
        };

        return new RestoreStatusResponse(
            job.Id,
            statusStr,
            job.Progress,
            job.BytesTotal,
            job.BytesDone,
            job.LogTail,
            job.EtaSeconds);
    }

    public async Task<PendingRestoreResponse?> GetPendingForAgentAsync(Guid agentId)
    {
        var job = await _restoreJobRepository.GetPendingWithDetailsAsync(agentId);
        if (job is null) return null;

        // A pending restore that the agent should pick up must still have
        // a live artifact / backup job / policy. If the operator deleted
        // them out from under it (e.g. selective agent purge), skip it
        // rather than crash the agent's poll.
        var artifact = job.Artifact;
        var policy = artifact?.Job?.Policy;
        if (artifact is null || policy is null) return null;

        return new PendingRestoreResponse(
            job.Id,
            artifact.ObjectKey,
            artifact.FileName,
            MapPolicyType(policy.Type),
            policy.SourcePath,
            MapDatabaseSettings(policy.DatabaseSettings),
            artifact.Checksum);
    }

    public async Task<string> GetDownloadTicketAsync(
        Guid jobId,
        Guid agentId,
        string? publicServerBaseUrl,
        CancellationToken cancellationToken = default)
    {
        var job = await _restoreJobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Restore job {jobId} not found.");

        if (job.AgentId != agentId)
            throw new UnauthorizedAccessException("This restore job does not belong to the requesting agent.");

        if (job.Status == RestoreJobStatus.Completed || job.Status == RestoreJobStatus.Failed)
            throw new InvalidOperationException("Cannot download ticket for a finished restore job.");

        var artifactId = job.ArtifactId
            ?? throw new InvalidOperationException("Restore job has no artifact assigned.");
        var artifact = await _artifactRepository.GetArtifactByIdAsync(artifactId)
            ?? throw new KeyNotFoundException("Artifact not found.");

        if (job.Status == RestoreJobStatus.Pending)
        {
            job.Status = RestoreJobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            await _restoreJobRepository.UpdateAsync(job);
            await _restoreJobRepository.SaveChangesAsync();
        }

        return await _storageAccessService.CreateDownloadTicketAsync(
            artifact.ObjectKey,
            artifact.SizeBytes,
            publicServerBaseUrl,
            cancellationToken);
    }

    public async Task CompleteAsync(Guid jobId, Guid agentId)
    {
        var job = await GetOwnedJobAsync(jobId, agentId);
        job.Status = RestoreJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;
        await _restoreJobRepository.UpdateAsync(job);
        await _restoreJobRepository.SaveChangesAsync();
    }

    public async Task FailedAsync(Guid jobId, Guid agentId, string errorMessage)
    {
        var job = await GetOwnedJobAsync(jobId, agentId);
        job.Status = RestoreJobStatus.Failed;
        job.CompletedAt = DateTime.UtcNow;
        job.ErrorMessage = errorMessage;
        await _restoreJobRepository.UpdateAsync(job);
        await _restoreJobRepository.SaveChangesAsync();
        await _notificationService.NotifyRestoreFailedAsync(jobId, agentId, errorMessage);
    }

    private async Task<RestoreJob> GetOwnedJobAsync(Guid jobId, Guid agentId)
    {
        var job = await _restoreJobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Restore job {jobId} not found.");

        if (job.AgentId != agentId)
            throw new UnauthorizedAccessException("This restore job does not belong to the requesting agent.");

        return job;
    }

    private static string MapPolicyType(BackupPolicyType type) => type switch
    {
        BackupPolicyType.FileSystem => "filesystem",
        BackupPolicyType.PostgreSqlDump => "postgres",
        BackupPolicyType.MySqlDump => "mysql",
        _ => throw new ArgumentOutOfRangeException(nameof(type))
    };

    private static BackupPolicyDatabaseSettingsDto? MapDatabaseSettings(BackupPolicyDatabaseSettings? s)
    {
        if (s is null) return null;
        return new BackupPolicyDatabaseSettingsDto(
            s.Engine switch
            {
                DatabaseEngine.PostgreSql => "postgres",
                DatabaseEngine.MySql => "mysql",
                _ => throw new ArgumentOutOfRangeException(nameof(s.Engine))
            },
            s.AuthMode switch
            {
                DatabaseDumpAuthMode.Integrated => "integrated",
                DatabaseDumpAuthMode.Credentials => "credentials",
                _ => throw new ArgumentOutOfRangeException(nameof(s.AuthMode))
            },
            s.Host, s.Port, s.DatabaseName, s.Username, s.Password);
    }
}
