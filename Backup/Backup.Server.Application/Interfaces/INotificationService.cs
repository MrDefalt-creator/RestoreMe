namespace Backup.Server.Application.Interfaces;

public interface INotificationService
{
    Task NotifyBackupFailedAsync(Guid jobId, Guid policyId, Guid agentId, string errorMessage, CancellationToken cancellationToken = default);
}
