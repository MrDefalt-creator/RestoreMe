using Backup.Shared.Contracts.DTOs.Policies;

namespace Backup.Agent.Worker.Interfaces;

public interface IBackupExecutor
{
    Task ExecutePolicyAsync(BackupPolicyDto policy, CancellationToken cancellationToken);
}