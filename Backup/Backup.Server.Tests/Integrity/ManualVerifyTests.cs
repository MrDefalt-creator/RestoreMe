using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Server.Domain.Enums;
using Backup.Server.Infrastructure.Options;
using Backup.Shared.Contracts.DTOs.Jobs;
using Microsoft.Extensions.Options;

namespace Backup.Server.Tests.Integrity;

public sealed class ManualVerifyTests
{
    private static readonly Guid ArtifactId = Guid.NewGuid();

    [Fact]
    public async Task Match_ReturnsVerified_AndUpdates()
    {
        var repo = new StubRepo(new BackupArtifact { Id = ArtifactId, ObjectKey = "k", FileName = "f", SizeBytes = 10, Checksum = "ABC" });
        var service = new BackupArtifactsService(repo, new StubStorage("abc"), new StubAudit(), Options.Create(new StorageOptions()));

        var result = await service.VerifyArtifactAsync(ArtifactId, actorId: null, CancellationToken.None);

        Assert.Equal(nameof(ArtifactIntegrityStatus.Verified), result.IntegrityStatus);
        Assert.Equal(ArtifactIntegrityStatus.Verified, repo.LastStatus);
        Assert.NotNull(repo.LastVerifiedAt);
    }

    [Fact]
    public async Task Mismatch_ReturnsFailed()
    {
        var repo = new StubRepo(new BackupArtifact { Id = ArtifactId, ObjectKey = "k", FileName = "f", SizeBytes = 10, Checksum = "expected" });
        var service = new BackupArtifactsService(repo, new StubStorage("different"), new StubAudit(), Options.Create(new StorageOptions()));

        var result = await service.VerifyArtifactAsync(ArtifactId, actorId: null, CancellationToken.None);

        Assert.Equal(nameof(ArtifactIntegrityStatus.Failed), result.IntegrityStatus);
        Assert.Equal(ArtifactIntegrityStatus.Failed, repo.LastStatus);
        Assert.Null(repo.LastVerifiedAt);
    }

    private sealed class StubStorage : IStorageAccessService
    {
        private readonly string _hash;
        public StubStorage(string hash) { _hash = hash; }
        public Task<string> ComputeObjectSha256Async(string objectKey, CancellationToken ct) => Task.FromResult(_hash);
        public Task<StorageObjectInfo> GetObjectInfoAsync(string objectKey, CancellationToken ct) => throw new NotImplementedException();
        public Task EnsureBucketExistsAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<UploadTicketResponse> CreateUploadTicketAsync(Guid backupJobId, Guid policyId, Guid agentId, string fileName, string contentType, long sizeBytes, string? publicServerBaseUrl, CancellationToken ct) => throw new NotImplementedException();
        public Task WriteObjectToAsync(string objectKey, Stream destination, CancellationToken ct) => throw new NotImplementedException();
        public Task<string> CreateDownloadTicketAsync(string objectKey, long sizeBytes, string? publicServerBaseUrl, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteObjectAsync(string objectKey, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class StubRepo : IBackupArtifactRepository
    {
        private readonly BackupArtifact _artifact;
        public ArtifactIntegrityStatus? LastStatus { get; private set; }
        public DateTime? LastVerifiedAt { get; private set; }
        public StubRepo(BackupArtifact artifact) { _artifact = artifact; }
        public Task<BackupArtifact?> GetArtifactByIdAsync(Guid artifactId) => Task.FromResult<BackupArtifact?>(_artifact);
        public Task UpdateIntegrityAsync(Guid id, ArtifactIntegrityStatus status, DateTime? lastVerifiedAt, CancellationToken ct)
        { LastStatus = status; LastVerifiedAt = lastVerifiedAt; return Task.CompletedTask; }
        public Task<List<BackupArtifact>> GetAllArtifactsAsync() => throw new NotImplementedException();
        public Task<PagedResult<BackupArtifact>> QueryArtifactsAsync(PagedQuery query, CancellationToken ct) => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsByJobIdAsync(Guid jobId) => throw new NotImplementedException();
        public Task<int> CountByJobIdAsync(Guid jobId) => throw new NotImplementedException();
        public Task AddArtifact(BackupArtifact artifact) => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsForRetentionAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<List<BackupArtifact>> GetArtifactsForScrubAsync(int batchSize, CancellationToken ct) => throw new NotImplementedException();
        public Task DeleteArtifactAsync(Guid id, CancellationToken ct) => throw new NotImplementedException();
        public Task SaveChanges() => throw new NotImplementedException();
    }

    private sealed class StubAudit : IAuditLogRepository
    {
        public Task AddAsync(AuditLog log) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken ct) => throw new NotImplementedException();
    }
}
