using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class AppUserRepository : IAppUserRepository
{
    private readonly AppDbContext _dbContext;

    public AppUserRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<List<AppUser>> GetAllAsync()
    {
        return _dbContext.AppUsers
            .AsNoTracking()
            .OrderBy(x => x.Username)
            .ToListAsync();
    }

    public Task<AppUser?> GetByIdAsync(Guid id)
    {
        return _dbContext.AppUsers.FirstOrDefaultAsync(x => x.Id == id);
    }

    public Task<AppUser?> GetByNormalizedUsernameAsync(string normalizedUsername)
    {
        return _dbContext.AppUsers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.NormalizedUsername == normalizedUsername);
    }

    public Task<int> CountAsync()
    {
        return _dbContext.AppUsers.CountAsync();
    }

    public Task<int> CountActiveByRoleAsync(AppUserRole role)
    {
        return _dbContext.AppUsers.CountAsync(x => x.IsActive && x.Role == role);
    }

    public async Task AddAsync(AppUser user)
    {
        await _dbContext.AppUsers.AddAsync(user);
    }

    public Task UpdateAsync(AppUser user)
    {
        _dbContext.AppUsers.Update(user);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(AppUser user)
    {
        _dbContext.AppUsers.Remove(user);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync()
    {
        return _dbContext.SaveChangesAsync();
    }
}
