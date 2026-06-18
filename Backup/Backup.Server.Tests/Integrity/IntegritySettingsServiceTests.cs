using Backup.Server.Application.Interfaces;
using Backup.Server.Application.Services;
using Backup.Server.Domain.Entities;
using Backup.Shared.Contracts.DTOs.Integrity;

namespace Backup.Server.Tests.Integrity;

public sealed class IntegritySettingsServiceTests
{
    [Fact]
    public async Task Update_PersistsValues_AndRecomputesNextRun()
    {
        var repo = new InMemorySettingsRepo();
        var service = new IntegritySettingsService(repo, new StubAudit());

        var result = await service.UpdateAsync(
            new UpdateIntegrityScrubSettingsRequest(IsEnabled: false, IntervalDays: 1, RunAtMinutesUtc: 600, BatchSize: 25),
            actorId: null, CancellationToken.None);

        Assert.False(result.IsEnabled);
        Assert.Equal(1, result.IntervalDays);
        Assert.Equal(600, result.RunAtMinutesUtc);
        Assert.Equal(25, result.BatchSize);
        Assert.Equal(600, result.NextRunAt.Hour * 60 + result.NextRunAt.Minute); // 10:00 UTC
        Assert.Equal(25, repo.Saved!.BatchSize);
    }

    private sealed class InMemorySettingsRepo : IIntegrityScrubSettingsRepository
    {
        private IntegrityScrubSettings _row = new() { Id = Guid.NewGuid(), NextRunAt = DateTime.UtcNow };
        public IntegrityScrubSettings? Saved { get; private set; }
        public Task<IntegrityScrubSettings> GetOrCreateAsync(CancellationToken ct) => Task.FromResult(_row);
        public Task UpdateAsync(IntegrityScrubSettings settings, CancellationToken ct) { _row = settings; Saved = settings; return Task.CompletedTask; }
    }

    private sealed class StubAudit : IAuditLogRepository
    {
        public Task AddAsync(AuditLog log) => Task.CompletedTask;
        public Task SaveChangesAsync() => Task.CompletedTask;
        public Task<AuditLogQueryResult> QueryAsync(AuditLogQuery query, CancellationToken ct) => throw new NotImplementedException();
    }
}
