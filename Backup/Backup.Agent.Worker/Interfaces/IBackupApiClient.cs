using Backup.Shared.Contracts.DTOs;
using Backup.Shared.Contracts.DTOs.Jobs;

namespace Backup.Agent.Worker.Interfaces;

public interface IBackupApiClient
{
    Task<Guid> StartBackupJobAsync(Guid agentId, Guid policyId, CancellationToken cancellationToken);
    Task FinishBackupJobAsync(Guid jobId,CancellationToken cancellationToken);
    Task FailBackupJobAsync(Guid jobId, string errorMessage, CancellationToken cancellationToken);
    Task AddArtifactAsync(Guid jobId, string fileName, string objectKey, long size, string checksum, CancellationToken cancellationToken);
    
    Task<UploadTicketResponse> RequestUploadTicketAsync(
        RequestUploadTicketRequest request,
        CancellationToken cancellationToken);
}