using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
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

    public Task<int> CountByJobIdAsync(Guid jobId)
    {
        return _dbContext.BackupArtifacts.CountAsync(x => x.JobId == jobId);
    }

    public async Task AddArtifact(BackupArtifact artifact)
    {
        await _dbContext.BackupArtifacts.AddAsync(artifact);
    }

    public async Task<List<BackupArtifact>> GetArtifactsForRetentionAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.BackupArtifacts
            .Include(a => a.Job).ThenInclude(j => j.Policy)
            .Where(a => a.Job.Policy.RetentionDays != null
                || a.Job.Policy.RetentionMaxCount != null
                || a.Job.Policy.RetentionMaxTotalBytes != null)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<BackupArtifact>> GetArtifactsForScrubAsync(int batchSize, CancellationToken cancellationToken)
    {
        return await _dbContext.BackupArtifacts
            .AsNoTracking()
            .OrderBy(a => a.LastVerifiedAt == null ? 0 : 1)
            .ThenBy(a => a.LastVerifiedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateIntegrityAsync(Guid id, ArtifactIntegrityStatus status, DateTime? lastVerifiedAt, CancellationToken cancellationToken)
    {
        await _dbContext.BackupArtifacts
            .Where(a => a.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.IntegrityStatus, status)
                .SetProperty(a => a.LastVerifiedAt, lastVerifiedAt),
                cancellationToken);
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
