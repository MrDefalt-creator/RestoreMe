using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Tests.Integrity;

public sealed class IntegrityScrubSettingsRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<AppDbContext> _options = null!;
    private IDataProtectionProvider _dp = null!;

    public Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _dp = new EphemeralDataProtectionProvider();
        using var ctx = new AppDbContext(_options, _dp);
        ctx.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() { _connection.Dispose(); return Task.CompletedTask; }

    [Fact]
    public async Task GetOrCreate_CreatesDefaults_ThenReturnsSameRow()
    {
        var repo1 = new IntegrityScrubSettingsRepository(new AppDbContext(_options, _dp));
        var created = await repo1.GetOrCreateAsync(CancellationToken.None);

        Assert.True(created.IsEnabled);
        Assert.Equal(7, created.IntervalDays);
        Assert.Equal(180, created.RunAtMinutesUtc);
        Assert.Equal(50, created.BatchSize);
        Assert.True(created.NextRunAt > DateTime.UtcNow);

        var repo2 = new IntegrityScrubSettingsRepository(new AppDbContext(_options, _dp));
        var fetched = await repo2.GetOrCreateAsync(CancellationToken.None);
        Assert.Equal(created.Id, fetched.Id);

        using var ctx = new AppDbContext(_options, _dp);
        Assert.Equal(1, await ctx.IntegrityScrubSettings.CountAsync());
    }
}
