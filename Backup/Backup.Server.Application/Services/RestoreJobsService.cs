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

    public async Task<Guid> CreateRestoreAsync(Guid artifactId, CancellationToken cancellationToken = default)
    {
        var artifact = await _artifactRepository.GetArtifactByIdAsync(artifactId)
            ?? throw new KeyNotFoundException($"Artifact {artifactId} not found.");

        var backupJob = await _backupJobRepository.GetBackupJob(artifact.JobId)
            ?? throw new KeyNotFoundException($"Backup job {artifact.JobId} not found.");

        var job = new RestoreJob
        {
            Id = Guid.NewGuid(),
            ArtifactId = artifactId,
            AgentId = backupJob.AgentId,
            Status = RestoreJobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _restoreJobRepository.AddAsync(job);
        await _restoreJobRepository.SaveChangesAsync();

        return job.Id;
    }

    public async Task<PendingRestoreResponse?> GetPendingForAgentAsync(Guid agentId)
    {
        var job = await _restoreJobRepository.GetPendingWithDetailsAsync(agentId);
        if (job is null) return null;

        var policy = job.Artifact.Job.Policy;

        return new PendingRestoreResponse(
            job.Id,
            job.Artifact.ObjectKey,
            job.Artifact.FileName,
            MapPolicyType(policy.Type),
            policy.SourcePath,
            MapDatabaseSettings(policy.DatabaseSettings));
    }

    public async Task<string> GetDownloadTicketAsync(
        Guid jobId,
        Guid agentId,
        CancellationToken cancellationToken = default)
    {
        var job = await _restoreJobRepository.GetByIdAsync(jobId)
            ?? throw new KeyNotFoundException($"Restore job {jobId} not found.");

        if (job.AgentId != agentId)
            throw new UnauthorizedAccessException("This restore job does not belong to the requesting agent.");

        if (job.Status == RestoreJobStatus.Completed || job.Status == RestoreJobStatus.Failed)
            throw new InvalidOperationException("Cannot download ticket for a finished restore job.");

        var artifact = await _artifactRepository.GetArtifactByIdAsync(job.ArtifactId)
            ?? throw new KeyNotFoundException("Artifact not found.");

        if (job.Status == RestoreJobStatus.Pending)
        {
            job.Status = RestoreJobStatus.Running;
            job.StartedAt = DateTime.UtcNow;
            await _restoreJobRepository.UpdateAsync(job);
            await _restoreJobRepository.SaveChangesAsync();
        }

        return await _storageAccessService.CreateDownloadTicketAsync(artifact.ObjectKey, cancellationToken);
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
