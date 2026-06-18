using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class RestoreJobRepository : IRestoreJobRepository
{
    private readonly AppDbContext _db;

    public RestoreJobRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<RestoreJob?> GetByIdAsync(Guid id)
    {
        return await _db.RestoreJobs.FirstOrDefaultAsync(x => x.Id == id);
    }

    public async Task<RestoreJob?> GetPendingWithDetailsAsync(Guid agentId)
    {
        return await _db.RestoreJobs
            .Include(x => x.Artifact)
                .ThenInclude(a => a.Job)
                    .ThenInclude(j => j.Policy)
                        .ThenInclude(p => p.DatabaseSettings)
            .Where(x => x.AgentId == agentId && x.Status == RestoreJobStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(RestoreJob job)
    {
        await _db.RestoreJobs.AddAsync(job);
    }

    public async Task UpdateAsync(RestoreJob job)
    {
        _db.RestoreJobs.Update(job);
    }

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }
}
