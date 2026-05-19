using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task SaveChangesAsync();
}
