using Backup.Server.Domain.Entities;

namespace Backup.Server.Application.Interfaces;

public interface IBackupArtifactRepository
{
    public Task<List<BackupArtifact>> GetAllArtifactsAsync();
    
    public Task<List<BackupArtifact>> GetArtifactsByJobIdAsync(Guid jobId);

    public Task<BackupArtifact?> GetArtifactByIdAsync(Guid artifactId);

    public Task<int> CountByJobIdAsync(Guid jobId);

    public Task AddArtifact(BackupArtifact artifact);

    // All artifacts belonging to policies that have at least one retention rule
    // configured (days / max-count / max-total-bytes). The caller (RetentionEvaluator)
    // decides which of these to prune. Includes Job + Policy for evaluation.
    public Task<List<BackupArtifact>> GetArtifactsForRetentionAsync(CancellationToken cancellationToken);

    public Task DeleteArtifactAsync(Guid id, CancellationToken cancellationToken);

    public Task SaveChanges();
}
