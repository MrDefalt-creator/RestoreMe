using Backup.Server.Api.Security;
using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Options;
using Backup.Server.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backup.Server.Tests.Auth;

/// <summary>
/// Integration tests for RefreshTokenService. Mirrors the SQLite in-memory +
/// EphemeralDataProtectionProvider fixture used by RefreshTokenRepositoryTests,
/// but drives a real RefreshTokenRepository through the service so
/// FindByHashAsync/RevokeFamilyAsync actually persist across rotation steps.
/// </summary>
public sealed class RefreshTokenServiceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private DbContextOptions<AppDbContext> _options = null!;
    private IDataProtectionProvider _dataProtection = null!;

    public Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dataProtection = new EphemeralDataProtectionProvider();

        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    private AppDbContext CreateContext() => new(_options, _dataProtection);

    private static RefreshTokenService CreateService(AppDbContext ctx)
    {
        var repo = new RefreshTokenRepository(ctx);
        return new RefreshTokenService(repo, Options.Create(new JwtOptions()));
    }

    private static async Task<Guid> SeedUser(AppDbContext ctx)
    {
        var userId = Guid.NewGuid();
        ctx.AppUsers.Add(new AppUser
        {
            Id = userId,
            Username = "refresh-svc-user",
            NormalizedUsername = "REFRESH-SVC-USER",
            PasswordHash = "irrelevant-hash",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return userId;
    }

    [Fact]
    public void Hash_is_deterministic_hex_64()
    {
        var h = RefreshTokenService.Hash("abc");
        Assert.Equal(64, h.Length);
        Assert.Equal(h, RefreshTokenService.Hash("abc"));
    }

    [Fact]
    public async Task Replaying_a_rotated_token_revokes_the_family()
    {
        // Arrange: issue T1, rotate -> T2 (T1 now revoked+ReplacedBy).
        await using var ctx = CreateContext();
        var service = CreateService(ctx);
        var userId = await SeedUser(ctx);
        var familyId = Guid.NewGuid();

        var t1 = await service.IssueAsync(userId, familyId, "agent-a", "127.0.0.1", CancellationToken.None);
        await ctx.SaveChangesAsync();

        var rotation1 = await service.RotateAsync(t1.RawToken, "agent-a", "127.0.0.1", CancellationToken.None);
        Assert.True(rotation1.Ok);
        Assert.False(rotation1.ReuseDetected);
        Assert.NotNull(rotation1.RawToken);

        // Act: replay T1, which is no longer active (it was rotated away).
        var replay = await service.RotateAsync(t1.RawToken, "attacker", "10.0.0.1", CancellationToken.None);

        // Assert: reuse detected, rotation rejected, and the WHOLE family
        // (including the legitimately-rotated T2) is now burned.
        Assert.False(replay.Ok);
        Assert.True(replay.ReuseDetected);
        Assert.Null(replay.RawToken);

        var repo = new RefreshTokenRepository(ctx);
        var active = await repo.GetActiveForUserAsync(userId);
        Assert.Empty(active);
    }
}
