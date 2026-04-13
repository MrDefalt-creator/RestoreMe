using Backup.Server.Application.Interfaces;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;

namespace Backup.Server.Infrastructure.Services;

public class BackupArtifactRepository : IBackupArtifactRepository
{
    private readonly AppDbContext _dbContext;

    public BackupArtifactRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
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