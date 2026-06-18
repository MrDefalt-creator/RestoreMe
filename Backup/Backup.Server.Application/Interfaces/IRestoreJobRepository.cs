using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IRestoreJobRepository
{
    Task<RestoreJob?> GetByIdAsync(Guid id);
    Task<RestoreJob?> GetPendingWithDetailsAsync(Guid agentId);
    Task AddAsync(RestoreJob job);
    Task UpdateAsync(RestoreJob job);
    Task SaveChangesAsync();
}
