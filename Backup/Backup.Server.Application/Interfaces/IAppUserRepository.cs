using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;

namespace Backup.Server.Application.Interfaces;

public interface IAppUserRepository
{
    Task<List<AppUser>> GetAllAsync();
    Task<AppUser?> GetByIdAsync(Guid id);
    Task<AppUser?> GetByNormalizedUsernameAsync(string normalizedUsername);
    Task<int> CountAsync();
    Task<int> CountActiveByRoleAsync(AppUserRole role);
    Task AddAsync(AppUser user);
    Task UpdateAsync(AppUser user);
    Task DeleteAsync(AppUser user);
    Task SaveChangesAsync();
}
