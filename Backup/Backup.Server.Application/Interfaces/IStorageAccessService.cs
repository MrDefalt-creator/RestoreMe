using Backup.Shared.Contracts.DTOs.Jobs;

namespace Backup.Server.Application.Interfaces;

public interface IStorageAccessService
{
    Task EnsureBucketExistsAsync(CancellationToken cancellationToken);

    Task<UploadTicketResponse> CreateUploadTicketAsync(
        Guid backupJobId,
        Guid policyId,
        Guid agentId,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);
}