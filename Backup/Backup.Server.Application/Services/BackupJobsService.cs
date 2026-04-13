using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Application.Services;

public class BackupJobsService
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IAgentRepository _agentRepository;
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly IBackupArtifactRepository _backupArtifactRepository;
    
    public BackupJobsService(IPolicyRepository policyRepository, IAgentRepository agentRepository, IBackupJobRepository backupJobRepository, IBackupArtifactRepository backupArtifactRepository)
    {
        _policyRepository = policyRepository;
        _agentRepository = agentRepository;
        _backupJobRepository = backupJobRepository;
        _backupArtifactRepository = backupArtifactRepository;
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
        };
        
        await _backupJobRepository.AddBackupJob(backupJob);
        await _backupJobRepository.SaveChangesAsync();
        
        return backupJob.Id;
        
    }

    public async Task Complete(Guid jobId)
    {
        var job = await _backupJobRepository.GetBackupJob(jobId);

        if (job == null)
        {
            throw new ApplicationException($"Job with id {jobId} does not exist");
        }

        job.Status = BackupJobStatus.Completed;
        job.CompletedAt = DateTime.UtcNow;

        await _backupJobRepository.UpdateBackupJob(job);
        await _backupJobRepository.SaveChangesAsync();

    }

    public async Task Failed(Guid jobId, string errorMessage)
    {
        var job = await _backupJobRepository.GetBackupJob(jobId);
        if (job == null)
        {
            throw new ApplicationException($"Job with id {jobId} does not exist");
        }
        
        job.CompletedAt = DateTime.UtcNow;
        job.Status = BackupJobStatus.Failed;
        job.ErrorMessage = errorMessage;
        
        await _backupJobRepository.UpdateBackupJob(job);
        await _backupJobRepository.SaveChangesAsync();
    }

    public async Task AddArtifact(Guid jobId, string fileName, string objectKey, long size, string checksum)
    {
        var job = await _backupJobRepository.GetBackupJob(jobId);
        if (job == null)
        {
            throw new ApplicationException($"Job with id {jobId} does not exist");
        }

        var BackupArtifact = new BackupArtifact
        {
            Id = Guid.NewGuid(),
            FileName = fileName,
            SizeBytes = size,
            ObjectKey = objectKey,
            Checksum = checksum,
            JobId = jobId,
        };
        
        await _backupArtifactRepository.AddArtifact(BackupArtifact);
        await _backupArtifactRepository.SaveChanges();
        
    }
}