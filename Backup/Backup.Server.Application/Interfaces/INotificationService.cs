namespace Backup.Server.Application.Interfaces;

public interface INotificationService
{
    Task NotifyBackupFailedAsync(Guid jobId, Guid policyId, Guid agentId, string errorMessage, CancellationToken cancellationToken = default);

    Task NotifyRestoreFailedAsync(Guid jobId, Guid agentId, string errorMessage, CancellationToken cancellationToken = default);

    Task NotifyBackupCompletedAsync(Guid jobId, Guid policyId, Guid agentId, string policyName, CancellationToken cancellationToken = default);

    Task NotifyAgentOfflineAsync(Guid agentId, string agentName, DateTime? lastSeenAt, CancellationToken cancellationToken = default);

    Task NotifyAgentBackOnlineAsync(Guid agentId, string agentName, CancellationToken cancellationToken = default);
}
