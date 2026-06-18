using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs;
using Backup.Shared.Contracts.DTOs.Jobs;

namespace Backup.Server.Application.Services;

public class BackupJobsService
{
    private const int AutoDisableThreshold = 3;

    private readonly IPolicyRepository _policyRepository;
    private readonly IAgentRepository _agentRepository;
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly IBackupArtifactRepository _backupArtifactRepository;
    private readonly IStorageAccessService _storageAccessService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogRepository _auditLogRepository;

    public BackupJobsService(
        IPolicyRepository policyRepository,
        IAgentRepository agentRepository,
        IBackupJobRepository backupJobRepository,
        IBackupArtifactRepository backupArtifactRepository,
        IStorageAccessService storageAccessService,
        INotificationService notificationService,
        IAuditLogRepository auditLogRepository)
    {
        _policyRepository = policyRepository;
        _agentRepository = agentRepository;
        _backupJobRepository = backupJobRepository;
        _backupArtifactRepository = backupArtifactRepository;
        _storageAccessService = storageAccessService;
        _notificationService = notificationService;
        _auditLogRepository = auditLogRepository;
    }
    
    public async Task<List<BackupJob>> GetAllJobs()
    {
        return await _backupJobRepository.GetAllBackupJobsAsync();
    }
    
    public async Task<BackupJob> GetJobById(Guid jobId)
    {
        var job = await _backupJobRepository.GetBackupJob(jobId);
        if (job == null)
        {
            throw new ApplicationException($"Job with id {jobId} does not exist");
        }

        return job;
    }
    
    public async Task<List<BackupJob>> GetJobsByAgentId(Guid agentId)
    {
        return await _backupJobRepository.GetBackupJobsByAgentIdAsync(agentId);
    }
    
    public async Task<List<BackupJob>> GetJobsByPolicyId(Guid policyId)
    {
        return await _backupJobRepository.GetBackupJobsByPolicyIdAsync(policyId);
    }

    public async Task<Guid> Start(Guid agentId, Guid policyId)
    {
        var agent = await _agentRepository.GetAgentByIdAsync(agentId);

        if (agent == null)
        {
            throw new ApplicationException($"Agent with id {agentId} does not exist");
        }

        var policy = await _policyRepository.GetPolicyById(policyId);

        if (policy == null)
        {
            throw new ApplicationException($"Policy with id {policyId} does not exist");
        }

        var backupJob = new BackupJob
        {
            Id = Guid.NewGuid(),
            Status = BackupJobStatus.Running,
            PolicyId = policyId,
            AgentId = agentId,
        };

        await _backupJobRepository.AddBackupJob(backupJob);
        await _auditLogRepository.AddAsync(Audit(
            agentId,
            "job.started",
            backupJob.Id,
            $"policy={policy.Name} agent={agent.Name}"));
        await _backupJobRepository.SaveChangesAsync();

        return backupJob.Id;
    }

    public async Task Complete(Guid jobId)
    {
        // Re-fetch job + artifacts inside a single transaction so a parallel
        // AddArtifact can't see "running" and let us mark complete with a stale
        // artifact-count read. Postgres default isolation (READ COMMITTED) is
        // enough — the artifact INSERT either commits before our SELECT or
        // serializes after our UPDATE.
        Guid policyId = Guid.Empty;
        Guid agentId = Guid.Empty;
        string policyName = string.Empty;
        var shouldNotify = false;

        await _backupJobRepository.ExecuteInTransactionAsync(async () =>
        {
            var job = await _backupJobRepository.GetBackupJob(jobId)
                ?? throw new KeyNotFoundException($"Job with id {jobId} does not exist");

            var artifactCount = await _backupArtifactRepository.CountByJobIdAsync(jobId);
            if (artifactCount == 0)
            {
                throw new InvalidOperationException("Backup job cannot be completed before a verified artifact is registered.");
            }

            // Idempotent: a retried Complete from a flaky agent must not
            // re-fire the completion notification. The first call wins.
            if (job.Status == BackupJobStatus.Completed)
            {
                return;
            }

            job.Status = BackupJobStatus.Completed;
            job.CompletedAt = DateTime.UtcNow;
            job.ErrorMessage = null;

            await _backupJobRepository.UpdateBackupJob(job);
            await _auditLogRepository.AddAsync(Audit(
                RequireAgentId(job),
                "job.completed",
                job.Id,
                $"policy={job.PolicyId} artifacts={artifactCount}"));

            // A green run wipes the failure streak so the next isolated
            // failure doesn't trip auto-disable.
            var completedPolicyId = RequirePolicyId(job);
            var policy = await _policyRepository.GetPolicyById(completedPolicyId);
            if (policy is not null && (policy.ConsecutiveFailureCount > 0 || policy.LastFailureReason is not null || policy.AutoDisabledAt is not null))
            {
                policy.ConsecutiveFailureCount = 0;
                policy.LastFailureReason = null;
                policy.AutoDisabledAt = null;
                await _policyRepository.UpdatePolicy(policy);
            }

            await _backupJobRepository.SaveChangesAsync();

            policyId = completedPolicyId;
            agentId = RequireAgentId(job);
            shouldNotify = true;
        });

        if (shouldNotify)
        {
            var policy = await _policyRepository.GetPolicyById(policyId);
            policyName = policy?.Name ?? string.Empty;
            await _notificationService.NotifyBackupCompletedAsync(jobId, policyId, agentId, policyName);
        }
    }

    public async Task Failed(Guid jobId, string errorMessage)
    {
        var job = await _backupJobRepository.GetBackupJob(jobId)
            ?? throw new KeyNotFoundException($"Job with id {jobId} does not exist");

        // Idempotent: a re-tried Failed call from a flaky agent must not
        // re-fire the webhook or rewrite the original error message.
        if (job.Status == BackupJobStatus.Failed)
        {
            return;
        }

        var failedAgentId = RequireAgentId(job);
        var failedPolicyId = RequirePolicyId(job);
        var truncatedReason = TruncateForAudit(errorMessage);

        // Track the failure streak. When the same policy keeps failing we flip
        // it off so a broken source/credentials doesn't spam the audit log +
        // notifications every interval. The counter bump and the conditional
        // auto-disable are set-based atomic DB updates (see PolicyRepository)
        // so concurrent failures of the same policy can't lose an increment or
        // double-fire the auto-disable. All of it shares one transaction with
        // the job-status write so a mid-flight failure rolls everything back.
        var policyAutoDisabled = false;
        string policyName = string.Empty;
        int failureCount = 0;

        await _backupJobRepository.ExecuteInTransactionAsync(async () =>
        {
            job.CompletedAt = DateTime.UtcNow;
            job.Status = BackupJobStatus.Failed;
            job.ErrorMessage = errorMessage;
            await _backupJobRepository.UpdateBackupJob(job);
            await _auditLogRepository.AddAsync(Audit(
                failedAgentId,
                "job.failed",
                job.Id,
                $"policy={failedPolicyId} error={truncatedReason}"));

            await _policyRepository.IncrementFailureStreakAsync(failedPolicyId, truncatedReason);
            policyAutoDisabled = await _policyRepository.TryAutoDisableAsync(
                failedPolicyId,
                AutoDisableThreshold,
                DateTime.UtcNow);

            if (policyAutoDisabled)
            {
                var policy = await _policyRepository.GetPolicyById(failedPolicyId);
                failureCount = policy?.ConsecutiveFailureCount ?? AutoDisableThreshold;
                policyName = policy?.Name ?? string.Empty;

                await _auditLogRepository.AddAsync(Audit(
                    failedAgentId,
                    "policy.auto_disabled",
                    failedPolicyId,
                    $"failures={failureCount} reason={truncatedReason}"));
            }

            await _backupJobRepository.SaveChangesAsync();
        });

        await _notificationService.NotifyBackupFailedAsync(jobId, failedPolicyId, failedAgentId, errorMessage);

        if (policyAutoDisabled)
        {
            await _notificationService.NotifyPolicyAutoDisabledAsync(
                failedPolicyId,
                failedAgentId,
                policyName,
                failureCount,
                truncatedReason);
        }
    }

    public async Task AddArtifact(
        Guid jobId,
        string fileName,
        string objectKey,
        long size,
        string checksum,
        CancellationToken cancellationToken = default)
    {
        var job = await _backupJobRepository.GetBackupJob(jobId);
        if (job == null)
        {
            throw new ApplicationException($"Job with id {jobId} does not exist");
        }

        if (job.Status != BackupJobStatus.Running)
        {
            throw new InvalidOperationException("Backup job is not running.");
        }

        if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
        {
            throw new InvalidOperationException("Artifact file name must not contain directory path components.");
        }

        var expectedObjectPrefix = $"{RequireAgentId(job)}/{RequirePolicyId(job)}/{job.Id}/";
        if (!objectKey.StartsWith(expectedObjectPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Artifact object key does not belong to this backup job.");
        }

        if (size <= 0)
        {
            throw new InvalidOperationException("Artifact size must be greater than zero bytes.");
        }

        var objectInfo = await _storageAccessService.GetObjectInfoAsync(objectKey, cancellationToken);
        if (objectInfo.SizeBytes != size)
        {
            throw new InvalidOperationException("Artifact size does not match the uploaded object.");
        }

        var backupArtifact = new BackupArtifact
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            SizeBytes = size,
            ObjectKey = objectKey,
            Checksum = checksum,
            JobId = jobId,
        };

        await _backupArtifactRepository.AddArtifact(backupArtifact);
        await _auditLogRepository.AddAsync(Audit(
            RequireAgentId(job),
            "artifact.added",
            backupArtifact.Id,
            $"job={jobId} size={size}"));
        await _backupArtifactRepository.SaveChanges();
    }

    private static AuditLog Audit(Guid actorId, string action, Guid? targetId = null, string? details = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            Action = action,
            TargetId = targetId,
            Details = details,
            OccurredAt = DateTime.UtcNow
        };

    private static string TruncateForAudit(string text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        const int max = 240;
        return text.Length <= max ? text : text[..max] + "…";
    }

    // BackupJob.AgentId/PolicyId are nullable in the schema so detached
    // history rows can survive a "keep history" agent delete. Any job
    // still executing (Running / Completing / Failing / Adding artifacts)
    // is guaranteed to have those FKs set — calling these helpers on a
    // detached row is a programming error, hence the exception.
    private static Guid RequireAgentId(BackupJob job) =>
        job.AgentId ?? throw new InvalidOperationException($"Backup job {job.Id} has no agent assigned.");

    private static Guid RequirePolicyId(BackupJob job) =>
        job.PolicyId ?? throw new InvalidOperationException($"Backup job {job.Id} has no policy assigned.");
    
    public async Task<UploadTicketResponse> RequestUploadTicketAsync(
        RequestUploadTicketRequest request,
        string? publicServerBaseUrl = null)
    {
        var job = await _backupJobRepository.GetBackupJob(request.BackupJobId);
        if (job == null)
        {
            throw new KeyNotFoundException("Backup job not found.");
        }

        if (job.PolicyId != request.PolicyId)
        {
            throw new InvalidOperationException("Backup job does not belong to policy.");
        }

        if (job.Status != BackupJobStatus.Running)
        {
            throw new InvalidOperationException("Backup job is not running.");
        }

        return await _storageAccessService.CreateUploadTicketAsync(
            request.BackupJobId,
            request.PolicyId,
            RequireAgentId(job),
            request.FileName,
            request.ContentType,
            request.SizeBytes,
            publicServerBaseUrl,
            CancellationToken.None);
    }
    
}
