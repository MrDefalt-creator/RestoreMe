using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class IntegrityScrubSettingsRepository : IIntegrityScrubSettingsRepository
{
    private readonly AppDbContext _dbContext;

    public IntegrityScrubSettingsRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IntegrityScrubSettings> GetOrCreateAsync(CancellationToken cancellationToken)
    {
        var existing = await _dbContext.IntegrityScrubSettings.FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var created = new IntegrityScrubSettings { Id = Guid.NewGuid() };
        created.NextRunAt = IntegrityScheduleCalculator.ComputeNextRun(
            DateTime.UtcNow, created.IntervalDays, created.RunAtMinutesUtc);

        _dbContext.IntegrityScrubSettings.Add(created);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    public async Task UpdateAsync(IntegrityScrubSettings settings, CancellationToken cancellationToken)
    {
        _dbContext.IntegrityScrubSettings.Update(settings);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
