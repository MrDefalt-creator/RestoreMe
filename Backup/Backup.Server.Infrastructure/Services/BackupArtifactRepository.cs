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
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }
    
    public async Task<List<BackupArtifact>> GetArtifactsByJobIdAsync(Guid jobId)
    {
        return await _dbContext.BackupArtifacts
            .Where(x => x.JobId == jobId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
    }

    public async Task<BackupArtifact?> GetArtifactByIdAsync(Guid artifactId)
    {
        return await _dbContext.BackupArtifacts
            .FirstOrDefaultAsync(x => x.Id == artifactId);
    }

    public async Task AddArtifact(BackupArtifact artifact)
    {
        await _dbContext.BackupArtifacts.AddAsync(artifact);
    }

    public async Task SaveChanges()
    {
        await _dbContext.SaveChangesAsync();
    }
}
