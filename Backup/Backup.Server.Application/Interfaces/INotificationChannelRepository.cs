using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface INotificationChannelRepository
{
    Task<List<NotificationChannel>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<List<NotificationChannel>> GetEnabledAsync(CancellationToken cancellationToken = default);

    Task<NotificationChannel?> GetByIdAsync(Guid channelId, CancellationToken cancellationToken = default);

    Task<NotificationChannel?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task AddAsync(NotificationChannel channel, CancellationToken cancellationToken = default);

    void Update(NotificationChannel channel);

    void Remove(NotificationChannel channel);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
