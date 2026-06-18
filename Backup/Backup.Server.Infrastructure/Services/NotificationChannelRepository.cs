using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class NotificationChannelRepository : INotificationChannelRepository
{
    private readonly AppDbContext _dbContext;

    public NotificationChannelRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<NotificationChannel>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.NotificationChannels
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<NotificationChannel>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.NotificationChannels
            .AsNoTracking()
            .Where(x => x.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationChannel?> GetByIdAsync(Guid channelId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.NotificationChannels
            .FirstOrDefaultAsync(x => x.Id == channelId, cancellationToken);
    }

    public async Task<NotificationChannel?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        return await _dbContext.NotificationChannels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == name, cancellationToken);
    }

    public async Task AddAsync(NotificationChannel channel, CancellationToken cancellationToken = default)
    {
        await _dbContext.NotificationChannels.AddAsync(channel, cancellationToken);
    }

    public void Update(NotificationChannel channel)
    {
        _dbContext.NotificationChannels.Update(channel);
    }

    public void Remove(NotificationChannel channel)
    {
        _dbContext.NotificationChannels.Remove(channel);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
