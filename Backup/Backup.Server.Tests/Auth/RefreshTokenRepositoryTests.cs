using Backup.Server.Domain.Entities;
using Backup.Server.Infrastructure.Configuration;
using Backup.Server.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Backup.Server.Tests.Auth;

/// <summary>
/// Integration tests for RefreshTokenRepository. Uses SQLite in-memory
/// (not EF InMemory) so the RefreshToken -> AppUser FK actually gets
/// enforced, mirroring the fixture shape used by
/// AgentRepositorySelectiveDeleteTests.
/// </summary>
public sealed class RefreshTokenRepositoryTests : IAsyncLifetime
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

    private static async Task SeedUser(AppDbContext ctx, Guid userId)
    {
        ctx.AppUsers.Add(new AppUser
        {
            Id = userId,
            Username = "refresh-user",
            NormalizedUsername = "REFRESH-USER",
            PasswordHash = "irrelevant-hash",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    [Fact]
    public async Task RevokeAllForUser_marks_active_tokens_revoked()
    {
        await using var ctx = CreateContext();
        var repo = new RefreshTokenRepository(ctx);
        var userId = Guid.NewGuid();
        await SeedUser(ctx, userId);
        await repo.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "h1",
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
        });
        await repo.SaveChangesAsync();

        await repo.RevokeAllForUserAsync(userId, DateTime.UtcNow);
        await repo.SaveChangesAsync();

        var active = await repo.GetActiveForUserAsync(userId);
        Assert.Empty(active);
    }

    [Fact]
    public async Task GetActiveForUser_excludes_expired_and_revoked_but_keeps_active()
    {
        await using var ctx = CreateContext();
        var repo = new RefreshTokenRepository(ctx);
        var userId = Guid.NewGuid();
        await SeedUser(ctx, userId);

        var activeId = Guid.NewGuid();
        await repo.AddAsync(new RefreshToken
        {
            Id = activeId,
            UserId = userId,
            TokenHash = "active",
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
        });
        await repo.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "expired",
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1),
        });
        await repo.AddAsync(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = "revoked",
            FamilyId = Guid.NewGuid(),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
            RevokedAtUtc = DateTime.UtcNow.AddMinutes(-1),
        });
        await repo.SaveChangesAsync();

        var active = await repo.GetActiveForUserAsync(userId);

        var activeToken = Assert.Single(active);
        Assert.Equal(activeId, activeToken.Id);
    }
}
