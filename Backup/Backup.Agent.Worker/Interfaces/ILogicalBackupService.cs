using Backup.Agent.Worker.DTOs;
using Backup.Shared.Contracts.DTOs.Policies;

namespace Backup.Agent.Worker.Interfaces;

public interface ILogicalBackupService
{
    Task<PreparedBackupPayload> CreateDumpAsync(BackupPolicyDto policy, CancellationToken cancellationToken);
}
