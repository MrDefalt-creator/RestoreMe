using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Shared.Contracts.DTOs;
using Backup.Shared.Contracts.DTOs.Jobs;

namespace Backup.Server.Application.Services;

public class BackupJobsService
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IAgentRepository _agentRepository;
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly IBackupArtifactRepository _backupArtifactRepository;
    private readonly IStorageAccessService  _storageAccessService; 
    
    public BackupJobsService(IPolicyRepository policyRepository, IAgentRepository agentRepository, IBackupJobRepository backupJobRepository, IBackupArtifactRepository backupArtifactRepository, IStorageAccessService storageAccessService)
    {
        _policyRepository = policyRepository;
        _agentRepository = agentRepository;
        _backupJobRepository = backupJobRepository;
        _backupArtifactRepository = backupArtifactRepository;
        _storageAccessService = storageAccessService;
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
        job.ErrorMessage = null;

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
        await _backupArtifactRepository.SaveChanges();
        
    }
    
    public async Task<UploadTicketResponse> RequestUploadTicketAsync(
        RequestUploadTicketRequest request,
        string? publicServerBaseUrl = null)
    {
        var job = await _backupJobRepository.GetBackupJob(request.BackupJobId);
        if (job == null)
        {
            throw new Exception("Backup job not found.");
        }

        if (job.PolicyId != request.PolicyId)
        {
            throw new Exception("Backup job does not belong to policy.");
        }

        if (job.Status != BackupJobStatus.Running)
        {
            throw new Exception("Backup job is not running.");
        }

        return await _storageAccessService.CreateUploadTicketAsync(
            request.BackupJobId,
            request.PolicyId,
            job.AgentId,
            request.FileName,
            request.ContentType,
            publicServerBaseUrl,
            CancellationToken.None);
    }
    
}
