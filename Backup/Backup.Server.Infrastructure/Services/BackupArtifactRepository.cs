using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Infrastructure.Services;

public class BackupArtifactRepository : IBackupArtifactRepository
{
    private readonly AppDbContext _dbContext;

    public BackupArtifactRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<List<BackupArtifact>> GetAllArtifactsAsync()
    {
        return await _dbContext.BackupArtifacts
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<List<BackupArtifact>> GetArtifactsByJobIdAsync(Guid jobId)
    {
        return await _dbContext.BackupArtifacts
            .AsNoTracking()
            .Where(x => x.JobId == jobId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<BackupArtifact?> GetArtifactByIdAsync(Guid artifactId)
    {
        return await _dbContext.BackupArtifacts
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == artifactId);
    }

    public async Task AddArtifact(BackupArtifact artifact)
    {
        await _dbContext.BackupArtifacts.AddAsync(artifact);
    }

    public async Task<List<BackupArtifact>> GetExpiredArtifactsAsync(CancellationToken cancellationToken)
    {
        var candidates = await _dbContext.BackupArtifacts
            .Include(a => a.Job).ThenInclude(j => j.Policy)
            .Where(a => a.Job.Policy.RetentionDays != null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        return candidates
            .Where(a => a.CreatedAt < now.AddDays(-a.Job.Policy.RetentionDays!.Value))
            .ToList();
    }

    public async Task DeleteArtifactAsync(Guid id, CancellationToken cancellationToken)
    {
        await _dbContext.BackupArtifacts
            .Where(a => a.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task SaveChanges()
    {
        await _dbContext.SaveChangesAsync();
    }
}
