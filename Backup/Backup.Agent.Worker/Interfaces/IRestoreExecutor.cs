namespace Backup.Agent.Worker.Interfaces;

public interface IRestoreExecutor
{
    Task ExecutePendingAsync(Guid agentId, CancellationToken cancellationToken);
}
