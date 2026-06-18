using Backup.Shared.Contracts.DTOs.Restore;

namespace Backup.Agent.Worker.Interfaces;

public interface IRestoreApiClient
{
    Task<PendingRestoreResponse?> GetPendingRestoreAsync(Guid agentId, CancellationToken cancellationToken);
    Task<string> RequestDownloadTicketAsync(Guid jobId, CancellationToken cancellationToken);
    Task CompleteRestoreJobAsync(Guid jobId, CancellationToken cancellationToken);
    Task FailRestoreJobAsync(Guid jobId, string errorMessage, CancellationToken cancellationToken);
}
