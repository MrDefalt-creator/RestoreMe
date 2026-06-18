using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IIntegrityScrubSettingsRepository
{
    Task<IntegrityScrubSettings> GetOrCreateAsync(CancellationToken cancellationToken);
    Task UpdateAsync(IntegrityScrubSettings settings, CancellationToken cancellationToken);
}
