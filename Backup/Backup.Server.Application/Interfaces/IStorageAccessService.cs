using Backup.Shared.Contracts.DTOs.Jobs;

namespace Backup.Server.Application.Interfaces;

public interface IStorageAccessService
{
    Task<UploadTicketResponse> CreateUploadTicketAsync(
        Guid backupJobId,
        Guid policyId,
        Guid agentId,
        string fileName,
        string contentType,
        CancellationToken cancellationToken);

    Task<Stream> OpenDownloadStreamAsync(
        string objectKey,
        CancellationToken cancellationToken);
}
